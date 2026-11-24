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

    public float GetVisualScaleMultiplier(CarDefinition car)
    {
        if (car == null)
            return 1f;

        float extraScale = Mathf.Max(0.1f, car.scale);
        CarDefinition referenceCar = FindById("basic");

        if (referenceCar == null)
            referenceCar = GetCar(0);

        if (!TryGetVisualBounds(referenceCar, out Bounds referenceBounds) ||
            !TryGetVisualBounds(car, out Bounds carBounds))
            return extraScale;

        float referenceSize = Mathf.Max(referenceBounds.size.x, referenceBounds.size.z);
        float carSize = Mathf.Max(carBounds.size.x, carBounds.size.z);

        if (referenceSize <= 0.0001f || carSize <= 0.0001f)
            return extraScale;

        return referenceSize / carSize * extraScale;
    }

    private static bool TryGetVisualBounds(CarDefinition car, out Bounds bounds)
    {
        bounds = default;

        if (car == null || car.visualPrefab == null)
            return false;

        Transform root = car.visualPrefab.transform;
        Renderer[] renderers = car.visualPrefab.GetComponentsInChildren<Renderer>(true);

        Matrix4x4 carMatrix = Matrix4x4.TRS(
            car.visualLocalPosition,
            Quaternion.Euler(car.visualLocalEulerAngles),
            car.visualLocalScale
        );

        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer is ParticleSystemRenderer ||
                renderer is TrailRenderer ||
                renderer is LineRenderer)
                continue;

            Bounds localBounds = renderer.localBounds;
            Matrix4x4 matrix = carMatrix *
                root.worldToLocalMatrix *
                renderer.transform.localToWorldMatrix;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 signs = new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f
                );

                Vector3 point = matrix.MultiplyPoint3x4(
                    localBounds.center + Vector3.Scale(localBounds.extents, signs)
                );

                if (!hasBounds)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        return hasBounds;
    }
}