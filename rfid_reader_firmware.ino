
/*
WiFi is used for date
NFC card has to be formatted like this: start<amount>_YYYY-MM-DD, for example start5_2026-09-30
If card is valid, "P" will be sent to bluetooth. it will also rewrite the card with 1 less start amounts


  Libraries:
    - MFRC522              by GithubCommunity
    - ESP32-BLE-Keyboard    by T-vK                  https://github.com/T-vK/ESP32-BLE-Keyboard

  

 Pinout:
    RC522      ESP32
    ---------------------
    SDA(SS) -> GPIO 5
    SCK     -> GPIO 18
    MOSI    -> GPIO 23
    MISO    -> GPIO 19
    RST     -> GPIO 22
    3.3V    -> 3.3V   (RC522 ei siedä 5V!)
    GND     -> GND

  Piezo:
    Buzzer     ESP32
    ---------------------
    (+) / IO  -> GPIO 21     
    (-) / GND -> GND

*/


#include <SPI.h>
#include <MFRC522.h>
#include <WiFi.h>
#include <time.h>
#include <BleKeyboard.h>

// ---------------- Settings ----------------

// WiFi credentials (for date)
const char* WIFI_SSID     = "FILL_WIFI_NAME";
const char* WIFI_PASSWORD = "FILL_WIFI_PASSWORD";

// TIMEZONE(Finnish)
 EET-2EEST,M3.5.0/3,M10.5.0/4
const char* TZ_FINLAND = "EET-2EEST,M3.5.0/3,M10.5.0/4";


const char* NTP_SERVER_1 = "pool.ntp.org";
const char* NTP_SERVER_2 = "time.google.com";

#define WIFI_OFF_AFTER_SYNC true


const char* BLE_DEVICE_NAME    = "ESP32 RFID Keyboard";
const char* BLE_DEVICE_MANUFACTURER = "JoyIT-RC522";


#define RFID_SS_PIN   5
#define RFID_RST_PIN  22

#define BUZZER_PIN    21

 READ_COOLDOWN_MS = 3000;
 ---------------------------------------------

MFRC522 rfid(RFID_SS_PIN, RFID_RST_PIN);
BleKeyboard bleKeyboard(BLE_DEVICE_NAME, BLE_DEVICE_MANUFACTURER, 100);

unsigned long lastReadMillis = 0;
String lastUidRead = "";

void beepSuccess() {
  tone(BUZZER_PIN, 2200, 90);
  delay(100);
}


void beepError() {
  tone(BUZZER_PIN, 400, 70);
  delay(90);
  tone(BUZZER_PIN, 400, 70);
  delay(90);
}



bool syncTimeFromWifi() {
  Serial.print("Conneting WiFi");
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  unsigned long startAttempt = millis();
  while (WiFi.status() != WL_CONNECTED && millis() - startAttempt < 15000) {
    delay(300);
    Serial.print(".");
  }
  Serial.println();

  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("Wifi failed");
    return false;
  }

  Serial.println("WiFi connected, time search");


  configTzTime(TZ_FINLAND, NTP_SERVER_1, NTP_SERVER_2);

  struct tm timeinfo;
  int retries = 0;
  while (!getLocalTime(&timeinfo, 5000) && retries < 10) {
    Serial.println("Waiting for NTP sync");
    retries++;
  }

  if (retries >= 10) {
    Serial.println("NTP sync failed");
    return false;
  }

  Serial.print("Time synced: ");
  Serial.println(&timeinfo, "%Y-%m-%d %H:%M:%S");

  if (WIFI_OFF_AFTER_SYNC) {
    WiFi.disconnect(true);
    WiFi.mode(WIFI_OFF);
    Serial.println("WiFi disabled.");
  }

  return true;
}

bool getCurrentDate(int &year, int &month, int &day) {
  struct tm timeinfo;
  if (!getLocalTime(&timeinfo, 1000)) {
    return false;
  }
  year  = timeinfo.tm_year + 1900;
  month = timeinfo.tm_mon + 1;
  day   = timeinfo.tm_mday;
  return true;
}


enum CardKind { CARD_UNKNOWN, CARD_ULTRALIGHT, CARD_CLASSIC };
CardKind lastCardKind = CARD_UNKNOWN;


String readTextFromCard() {
  String result = "";
  lastCardKind = CARD_UNKNOWN;

  MFRC522::PICC_Type piccType = rfid.PICC_GetType(rfid.uid.sak);
  Serial.print("Card type: ");
  Serial.println(rfid.PICC_GetTypeName(piccType));

  if (piccType == MFRC522::PICC_TYPE_MIFARE_UL) {
    
    result = readNdefTextFromUltralight();
    lastCardKind = CARD_ULTRALIGHT;

  } else if (piccType == MFRC522::PICC_TYPE_MIFARE_1K ||
             piccType == MFRC522::PICC_TYPE_MIFARE_4K ||
             piccType == MFRC522::PICC_TYPE_MIFARE_MINI) {
    lastCardKind = CARD_CLASSIC;

    
    result = readRawTextFromClassicBlock(4);


    if (!result.startsWith("start")) {
      String ndefResult = readNdefTextFromClassic();
      if (ndefResult.length() > 0) {
        result = ndefResult;
      }
    }

  } else {
   
    Serial.println("Unknown card type! Trying NDEF/Ultralight");
    result = readNdefTextFromUltralight();
    if (result.length() > 0) {
      lastCardKind = CARD_ULTRALIGHT;
    }
  }

  return result;
}


bool writeTextToCard(const String &text) {
  if (lastCardKind == CARD_ULTRALIGHT) {
    return writeNdefTextToUltralight(text);
  } else if (lastCardKind == CARD_CLASSIC) {
    return writeRawTextToClassicBlock(4, text);
  }
  Serial.println("Unknown card type, writing failed");
  return false;
}


bool writeRawTextToClassicBlock(byte startBlockAddr, const String &text) {
  
  unsigned int totalBytesNeeded = text.length() + 1;
  byte blocksNeeded = (totalBytesNeeded + 15) / 16; 

  if (blocksNeeded > RAW_TEXT_MAX_BLOCKS) {
    Serial.println("Text is too long, even for many blocks Error.");
    return false;
  }

  byte blockAddr = startBlockAddr;
  unsigned int textPos = 0;

  for (byte b = 0; b < blocksNeeded; b++) {
   
    while (!isUsableClassicDataBlock(blockAddr)) {
      blockAddr++;
    }

    if (!authenticateBlockAnyKey(blockAddr, nullptr)) {
      Serial.print("Authentication failed for block");
      Serial.print(blockAddr);
      Serial.println(" with all tried keys.");
      return false;
    }

    byte buffer[16];
    memset(buffer, 0, sizeof(buffer));
    for (byte i = 0; i < 16 && textPos < text.length(); i++, textPos++) {
      buffer[i] = (byte)text.charAt(textPos);
    }
    

    MFRC522::StatusCode status = rfid.MIFARE_Write(blockAddr, buffer, 16);
    if (status != MFRC522::STATUS_OK) {
      Serial.print("Block ");
      Serial.print(blockAddr);
      Serial.print(" writing failed: ");
      Serial.println(rfid.GetStatusCodeName(status));
      return false;
    }

    blockAddr++;
  }

  return true;
}

// : 0x03 [len] [NDEF record] 0xFE).
bool writeNdefTextToUltralight(const String &text) {
  // NDEF text record: [header 0xD1][type len 0x01][payload len][type 'T'][status byte 0x02 = "en"][ "en" ][text]

  byte payloadLen = 1 + 2 + text.length(); // status byte + "en" + text
  byte ndefRecord[3 + 3 + text.length()];
  int idx = 0;
  ndefRecord[idx++] = 0xD1;               // record header: MB=1,ME=1,SR=1,TNF=1 (well-known)
  ndefRecord[idx++] = 0x01;               // type length = 1
  ndefRecord[idx++] = payloadLen;         // payload length
  ndefRecord[idx++] = 'T';                // type = Text
  ndefRecord[idx++] = 0x02;               // status byte: UTF-8, lang code length = 2
  ndefRecord[idx++] = 'e';
  ndefRecord[idx++] = 'n';
  for (unsigned int i = 0; i < text.length(); i++) {
    ndefRecord[idx++] = (byte)text.charAt(i);
  }

  byte ndefLen = idx; 

  // Will be written: [0x03][ndefLen][ndefRecord...][0xFE]
  int totalLen = 2 + ndefLen + 1;
  byte fullData[64];
  if (totalLen > (int)sizeof(fullData)) {
    Serial.println("Error: NDEF-message too long.");
    return false;
  }

  int p = 0;
  fullData[p++] = 0x03;
  fullData[p++] = ndefLen;
  memcpy(&fullData[p], ndefRecord, ndefLen);
  p += ndefLen;
  fullData[p++] = 0xFE; 

  
  int pagesNeeded = (p + 3) / 4;
  for (int pg = 0; pg < pagesNeeded; pg++) {
    byte pageBuf[4] = {0, 0, 0, 0};
    for (int b = 0; b < 4; b++) {
      int srcIdx = pg * 4 + b;
      if (srcIdx < p) pageBuf[b] = fullData[srcIdx];
    }
    MFRC522::StatusCode status = rfid.MIFARE_Ultralight_Write(4 + pg, pageBuf, 4);
    if (status != MFRC522::STATUS_OK) {
      Serial.print("NDEF page writing failed(page ");
      Serial.print(4 + pg);
      Serial.print("): ");
      Serial.println(rfid.GetStatusCodeName(status));
      return false;
    }
  }

  return true;
}


const byte NUM_TRIAL_KEYS = 6;
const byte TRIAL_KEYS[NUM_TRIAL_KEYS][6] = {
  {0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF}, // factory
  {0xD3, 0xF7, 0xD3, 0xF7, 0xD3, 0xF7}, // NFC Tools / NDEF (MAD) -avain A
  {0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5}, // common MAD-key
  {0x00, 0x00, 0x00, 0x00, 0x00, 0x00}, // zero key
  {0xB0, 0xB1, 0xB2, 0xB3, 0xB4, 0xB5}, // common MAD-key B
  {0x4D, 0x3A, 0x99, 0xC3, 0x51, 0xDD}  // another common NDEF-tool key
};


MFRC522::MIFARE_Key lastWorkingKey;
bool lastWorkingKeyIsB = false;
bool lastWorkingKeyValid = false;

bool authenticateBlockAnyKey(byte blockAddr, MFRC522::MIFARE_Key *outKey, bool *outIsKeyB) {

  if (lastWorkingKeyValid) {
    rfid.PCD_StopCrypto1();
    MFRC522::PICC_Command cachedCmd = lastWorkingKeyIsB
        ? MFRC522::PICC_CMD_MF_AUTH_KEY_B
        : MFRC522::PICC_CMD_MF_AUTH_KEY_A;

    MFRC522::StatusCode cachedStatus = rfid.PCD_Authenticate(
        cachedCmd, blockAddr, &lastWorkingKey, &(rfid.uid));

    if (cachedStatus == MFRC522::STATUS_OK) {
      if (outKey != nullptr) *outKey = lastWorkingKey;
      if (outIsKeyB != nullptr) *outIsKeyB = lastWorkingKeyIsB;
      return true;
    }
    if (cachedStatus == MFRC522::STATUS_TIMEOUT) {
      rfid.PICC_HaltA();
      for (byte retry = 0; retry < 3; retry++) {
        if (rfid.PICC_IsNewCardPresent() && rfid.PICC_ReadCardSerial()) break;
        delay(20);
      }
    }
  }

  for (byte k = 0; k < NUM_TRIAL_KEYS; k++) {
    for (byte keyTypeIdx = 0; keyTypeIdx < 2; keyTypeIdx++) {
      MFRC522::PICC_Command authCmd = (keyTypeIdx == 0)
          ? MFRC522::PICC_CMD_MF_AUTH_KEY_A
          : MFRC522::PICC_CMD_MF_AUTH_KEY_B;

    
      rfid.PCD_StopCrypto1();

      MFRC522::MIFARE_Key key;
      memcpy(key.keyByte, TRIAL_KEYS[k], 6);

      MFRC522::StatusCode status = rfid.PCD_Authenticate(
          authCmd, blockAddr, &key, &(rfid.uid));

      if (status == MFRC522::STATUS_OK) {
        if (outKey != nullptr) {
          memcpy(outKey->keyByte, TRIAL_KEYS[k], 6);
        }
        if (outIsKeyB != nullptr) {
          *outIsKeyB = (keyTypeIdx == 1);
        }
        
        memcpy(lastWorkingKey.keyByte, TRIAL_KEYS[k], 6);
        lastWorkingKeyIsB = (keyTypeIdx == 1);
        lastWorkingKeyValid = true;
        return true;
      }

      
      if (status == MFRC522::STATUS_TIMEOUT) {
        rfid.PICC_HaltA();
        for (byte retry = 0; retry < 3; retry++) {
          if (rfid.PICC_IsNewCardPresent() && rfid.PICC_ReadCardSerial()) {
            break;
          }
          delay(20);
        }
      }
    }
  }
  return false;
}


bool authenticateBlockAnyKey(byte blockAddr, MFRC522::MIFARE_Key *outKey) {
  return authenticateBlockAnyKey(blockAddr, outKey, nullptr);
}


bool isUsableClassicDataBlock(byte blockAddr) {
  if (blockAddr == 0) return false;   
  if ((blockAddr + 1) % 4 == 0) return false;
  return true;
}


String readNdefTextFromClassic() {
  byte allData[160];
  int dataLen = 0;

  for (byte blockAddr = 4; blockAddr < 40 && dataLen < (int)sizeof(allData) - 16; blockAddr++) {
    if (!isUsableClassicDataBlock(blockAddr)) continue;

    if (!authenticateBlockAnyKey(blockAddr, nullptr)) {
      break; 
    }

    byte buffer[18];
    byte size = sizeof(buffer);
    MFRC522::StatusCode status = rfid.MIFARE_Read(blockAddr, buffer, &size);
    if (status != MFRC522::STATUS_OK) break;

    for (int i = 0; i < 16 && dataLen < (int)sizeof(allData); i++) {
      allData[dataLen++] = buffer[i];
    }
  }

  return parseNdefTextFromBuffer(allData, dataLen);
}


String readRawTextFromClassicBlock(byte startBlockAddr) {
  String text = "";
  byte blockAddr = startBlockAddr;
  byte blocksRead = 0;

  while (blocksRead < RAW_TEXT_MAX_BLOCKS) {
    if (!isUsableClassicDataBlock(blockAddr)) {
      blockAddr++;
      continue;
    }

    if (!authenticateBlockAnyKey(blockAddr, nullptr)) {
      if (blocksRead == 0) {
        Serial.println("Auth failed with all tried keys");
        return "";
      }
      break;
    }

    byte buffer[18];
    byte size = sizeof(buffer);
    MFRC522::StatusCode status = rfid.MIFARE_Read(blockAddr, buffer, &size);

    if (status != MFRC522::STATUS_OK) {
      if (blocksRead == 0) {
        Serial.print("Block read failed ");
        Serial.println(rfid.GetStatusCodeName(status));
        return "";
      }
      break;
    }

    bool foundTerminator = false;
    for (byte i = 0; i < 16; i++) {
      if (buffer[i] == 0x00) { foundTerminator = true; break; } 
      if (buffer[i] >= 32 && buffer[i] <= 126) { 
        text += (char)buffer[i];
      }
    }

    blocksRead++;
    blockAddr++;

    if (foundTerminator) break; 
  }

  return text;
}


  for (int i = 0; i < dataLen - 1; i++) {
    if (allData[i] == 0x03) {
      int ndefLen = allData[i + 1];
      int ndefStart = i + 2;
      if (ndefStart + ndefLen > dataLen) continue;

      
      int p = ndefStart;
      if (p + 3 > dataLen) continue;
      byte typeLength = allData[p + 1];
      byte payloadLength = allData[p + 2];
      int headerSize = 3; 
      int typeStart = p + headerSize;
      if (typeStart >= dataLen) continue;

      if (allData[typeStart] == 'T') { /
        int payloadStart = typeStart + typeLength;
        if (payloadStart >= dataLen) continue;
        byte statusByte = allData[payloadStart];
        byte langCodeLen = statusByte & 0x3F;
        int textStart = payloadStart + 1 + langCodeLen;
        int textLen = payloadLength - 1 - langCodeLen;

        String text = "";
        for (int t = 0; t < textLen && (textStart + t) < dataLen; t++) {
          char c = (char)allData[textStart + t];
          if (c >= 32 && c <= 126) text += c;
        }
        return text;
      }
    }
  }

  return ""; 
}


String readNdefTextFromUltralight() {
  byte allData[160]; // 40 sivua * 4 tavua
  int dataLen = 0;

  for (byte page = 4; page < 44; page++) {
    byte buffer[18];
    byte size = sizeof(buffer);
    MFRC522::StatusCode status = rfid.MIFARE_Read(page, buffer, &size);
    if (status != MFRC522::STATUS_OK) {
      break;
    }
    for (int i = 0; i < 4 && dataLen < (int)sizeof(allData); i++) {
      allData[dataLen++] = buffer[i];
    }
  }

  return parseNdefTextFromBuffer(allData, dataLen);
}



bool parseStartTag(const String &text, int &amount, int &year, int &month, int &day) {
  String prefix = "start";
  if (!text.startsWith(prefix)) return false;

  int underscoreIdx = text.indexOf('_');
  if (underscoreIdx == -1) return false;

  String amountStr = text.substring(prefix.length(), underscoreIdx); 
  if (amountStr.length() == 0) return false;
  for (unsigned int i = 0; i < amountStr.length(); i++) {
    if (!isDigit(amountStr.charAt(i))) return false;
  }
  amount = amountStr.toInt();

  String rest = text.substring(underscoreIdx + 1); 

  int dash1 = rest.indexOf('-');
  int dash2 = rest.indexOf('-', dash1 + 1);

  if (dash1 == -1 || dash2 == -1) return false;

  String yearStr  = rest.substring(0, dash1);
  String monthStr = rest.substring(dash1 + 1, dash2);
  String dayStr   = rest.substring(dash2 + 1);

  if (yearStr.length() == 0 || monthStr.length() == 0 || dayStr.length() == 0) return false;

  year  = yearStr.toInt();
  month = monthStr.toInt();
  day   = dayStr.toInt();

  if (month < 1 || month > 12) return false;
  if (day < 1 || day > 31) return false;
  if (year < 2000 || year > 2100) return false;

  return true;
}


String buildStartTag(int amount, int year, int month, int day) {
  char buf[32];
  snprintf(buf, sizeof(buf), "start%d_%04d-%02d-%02d",
           amount, year, month, day);
  return String(buf);
}




void handleScanCommand() {
  Serial.println("Card to reader in 3 secs...");
  unsigned long waitStart = millis();
  bool cardFound = false;
  while (millis() - waitStart < 3000) {
    if (rfid.PICC_IsNewCardPresent() && rfid.PICC_ReadCardSerial()) {
      cardFound = true;
      break;
    }
    delay(50);
  }

  if (!cardFound) {
    Serial.println("card not found in given time");
    beepError();
    return;
  }

  Serial.print("Card found, UID: ");
  for (byte i = 0; i < rfid.uid.size; i++) {
    Serial.print(rfid.uid.uidByte[i], HEX);
  }
  Serial.println();

  MFRC522::PICC_Type piccType = rfid.PICC_GetType(rfid.uid.sak);
  Serial.print("Card type: ");
  Serial.println(rfid.PICC_GetTypeName(piccType));
  Serial.println();

  bool anyKeyFound = false;

  
  for (byte sector = 0; sector < 16; sector++) {
    byte trailerBlock = sector * 4 + 3;

    MFRC522::MIFARE_Key foundKey;
    bool isKeyB = false;
    bool ok = authenticateBlockAnyKey(trailerBlock, &foundKey, &isKeyB);

    Serial.print("Sector ");
    Serial.print(sector);
    Serial.print(" (blocks ");
    Serial.print(sector * 4);
    Serial.print("-");
    Serial.print(sector * 4 + 3);
    Serial.print("): ");

    if (ok) {
      anyKeyFound = true;
      Serial.print("Key found (");
      Serial.print(isKeyB ? "Key B" : "Key A");
      Serial.print(") = ");
      for (byte i = 0; i < 6; i++) {
        if (foundKey.keyByte[i] < 0x10) Serial.print("0");
        Serial.print(foundKey.keyByte[i], HEX);
        if (i < 5) Serial.print(" ");
      }
      Serial.println();
    } else {
      Serial.println("no key found");
    }

    rfid.PCD_StopCrypto1();
  }

  Serial.println();
  if (anyKeyFound) {
    Serial.println(">> Scanning finished, atleast on key found");
    beepSuccess();
  } else {
    Serial.println(">> Scanning finished, no key worked");
    
    beepError();
  }

 
  Serial.println();
  Serial.println("Block 4 rawdata (hex):");
  MFRC522::MIFARE_Key dumpKeyB;
  memcpy(dumpKeyB.keyByte, TRIAL_KEYS[0], 6); // FF FF FF FF FF FF
  MFRC522::StatusCode authStatus = rfid.PCD_Authenticate(
      MFRC522::PICC_CMD_MF_AUTH_KEY_B, 4, &dumpKeyB, &(rfid.uid));
  if (authStatus == MFRC522::STATUS_OK) {
    byte buf[18];
    byte size = sizeof(buf);
    MFRC522::StatusCode readStatus = rfid.MIFARE_Read(4, buf, &size);
    if (readStatus == MFRC522::STATUS_OK) {
      for (byte i = 0; i < 16; i++) {
        if (buf[i] < 0x10) Serial.print("0");
        Serial.print(buf[i], HEX);
        Serial.print(" ");
      }
      Serial.println();
      Serial.print("ASCII: '");
      for (byte i = 0; i < 16; i++) {
        char c = (char)buf[i];
        Serial.print((c >= 32 && c <= 126) ? c : '.');
      }
      Serial.println("'");
    } else {
      Serial.print("Block 4 reading failed in dump: ");
      Serial.println(rfid.GetStatusCodeName(readStatus));
    }
  } else {
    Serial.println("Block 4 auth failed in dump (Key B, FFFFFF...).");
  }
  rfid.PCD_StopCrypto1();

  rfid.PICC_HaltA();
  rfid.PCD_StopCrypto1();
}


void handleSerialCommand() {
  if (!Serial.available()) return;

  String line = Serial.readStringUntil('\n');
  line.trim();
  if (line.length() == 0) return;

  if (line == "scan") {
    handleScanCommand();
    return;
  }

  if (!line.startsWith("write")) {
    Serial.print("Unknown command: '");
    Serial.print(line);
    Serial.println("'. Usable commands: 'write <amount>' (like. write 5) or 'scan'");
    return;
  }

  
  int spaceIdx = line.indexOf(' ');
  if (spaceIdx == -1) {
    Serial.println("Invalid command. Use: write <amount> ");
    return;
  }

  String amountStr = line.substring(spaceIdx + 1);
  amountStr.trim();
  if (amountStr.length() == 0) {
    Serial.println("Invalid command. Use: write <amount>");
    return;
  }
  for (unsigned int i = 0; i < amountStr.length(); i++) {
    if (!isDigit(amountStr.charAt(i))) {
      Serial.println("Amount needs to be only numbers!");
      return;
    }
  }
  int amount = amountStr.toInt();
  if (amount <= 0) {
    Serial.println("Amount can't be zero!");
    return;
  }

  Serial.println("Card to reader in 3sec...");
  unsigned long waitStart = millis();
  bool cardFound = false;
  while (millis() - waitStart < 3000) {
    if (rfid.PICC_IsNewCardPresent() && rfid.PICC_ReadCardSerial()) {
      cardFound = true;
      break;
    }
    delay(50);
  }

  if (!cardFound) {
    Serial.println("Card not found in given time.");
    beepError();
    return;
  }

  Serial.print("Card found, UID: ");
  for (byte i = 0; i < rfid.uid.size; i++) {
    Serial.print(rfid.uid.uidByte[i], HEX);
  }
  Serial.println();

 
  MFRC522::PICC_Type piccType = rfid.PICC_GetType(rfid.uid.sak);
  Serial.print("Card type: ");
  Serial.println(rfid.PICC_GetTypeName(piccType));

  if (piccType == MFRC522::PICC_TYPE_MIFARE_UL) {
    lastCardKind = CARD_ULTRALIGHT;
  } else {
  
    lastCardKind = CARD_CLASSIC;
  }

  int curYear, curMonth, curDay;
  if (!getCurrentDate(curYear, curMonth, curDay)) {
    Serial.println("error, couldn't get current time");
    beepError();
    rfid.PICC_HaltA();
    rfid.PCD_StopCrypto1();
    return;
  }

  String newTag = buildStartTag(amount, curYear, curMonth, curDay);
  Serial.print("Writing to tag: ");
  Serial.println(newTag);

  bool writeOk = writeTextToCard(newTag);

  if (writeOk) {
    Serial.println(">> Success!");
    beepSuccess();
  } else {
    Serial.println(">> Writing failed, no key found");
    Serial.println(" Try "scan" to try to look for it");
    
    beepError();
  }

  rfid.PICC_HaltA();
  rfid.PCD_StopCrypto1();
}



void setup() {
  Serial.begin(115200);
  delay(500);
  Serial.println();
  Serial.println("=== ESP32 RFID -> BLE Keyboard ===");

  SPI.begin();
  rfid.PCD_Init();
  Serial.println("RC522 initialized.");


  pinMode(BUZZER_PIN, OUTPUT);
  noTone(BUZZER_PIN);

  
  bool timeOk = syncTimeFromWifi();
  if (!timeOk) {
    Serial.println("Error couldn't sync time. Now I won't function :angry:");
  }

  bleKeyboard.begin();
  Serial.println("BLE-keybo started, waiting for pairing");

  Serial.println();
  Serial.println("Serial commands:");
  Serial.println("  write <amount>");
  Serial.println("  scan");
  Serial.println("Card to reader after command send");
}

void loop() {
 
  handleSerialCommand();

 
  if (!bleKeyboard.isConnected()) {
    delay(200);
    return;
  }

 
  if (!rfid.PICC_IsNewCardPresent() || !rfid.PICC_ReadCardSerial()) {
    delay(100);
    return;
  }

 
  String uidStr = "";
  for (byte i = 0; i < rfid.uid.size; i++) {
    uidStr += String(rfid.uid.uidByte[i], HEX);
  }

  unsigned long now = millis();
  if (uidStr == lastUidRead && (now - lastReadMillis) < READ_COOLDOWN_MS) {
    rfid.PICC_HaltA();
    rfid.PCD_StopCrypto1();
    return;
  }
  lastUidRead = uidStr;
  lastReadMillis = now;

  Serial.print("Card found, UID: ");
  Serial.println(uidStr);

  String cardText = readTextFromCard();
  Serial.print("Text read: '");
  Serial.print(cardText);
  Serial.println("'");

  int cardAmount, cardYear, cardMonth, cardDay;
  if (parseStartTag(cardText, cardAmount, cardYear, cardMonth, cardDay)) {

    if (cardAmount <= 0) {
      
      Serial.println(">> Card empty!");
      beepError();
    } else {

      int curYear, curMonth, curDay;
      if (getCurrentDate(curYear, curMonth, curDay)) {

        bool dateMatches = (cardYear == curYear && cardMonth == curMonth && cardDay == curDay);

        if (dateMatches) {
          
          int newAmount = cardAmount - 1;
          String newTag = buildStartTag(newAmount, cardYear, cardMonth, cardDay);

          bool writeOk = writeTextToCard(newTag);

          if (writeOk) {
            
            bleKeyboard.print("P");
            Serial.println(">> Success");
          } else {
            Serial.println(">> Card writing failed, nothing happened");
          }

          Serial.print(">> Starts left on this card: ");
          Serial.println(newAmount);

          if (writeOk) {
            beepSuccess();
          } else {
            beepError();
          }
        } else {
          Serial.print(">> Date isn't matching. On card: ");
          Serial.print(cardYear); Serial.print("-");
          Serial.print(cardMonth); Serial.print("-");
          Serial.print(cardDay);
          Serial.print(" | Now: ");
          Serial.print(curYear); Serial.print("-");
          Serial.print(curMonth); Serial.print("-");
          Serial.println(curDay);
          beepError();
        }
      } else {
        Serial.println("time sync failed");
        beepError();
      }
    }
  } else {
    Serial.println(">> Card isn't formatted properly");
    beepError();
  }

  rfid.PICC_HaltA();
  rfid.PCD_StopCrypto1();

  delay(150);
}
