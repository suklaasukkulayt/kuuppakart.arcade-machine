using UnityEngine;

[CreateAssetMenu(fileName = "CarDatabase", menuName = "KuuppaKart/Cars/Car Database")]
public class CarDatabase : ScriptableObject
{
    public CarDefinition[] cars;

    public int Count
    {
        get { return cars != null ? cars.Length : 0; }
    }

    public CarDefinition GetCar(int index)
    {
        if (cars == null || index < 0 || index >= cars.Length)
            return null;

        return cars[index];
    }

    public int FindIndexById(string carId)
    {
        if (cars == null || cars.Length == 0)
            return -1;

        for (int i = 0; i < cars.Length; i++)
        {
            if (cars[i] != null && cars[i].StableId == carId)
                return i;
        }

        return -1;
    }

    public CarDefinition FindById(string carId)
    {
        int index = FindIndexById(carId);
        return index >= 0 ? cars[index] : null;
    }
}
