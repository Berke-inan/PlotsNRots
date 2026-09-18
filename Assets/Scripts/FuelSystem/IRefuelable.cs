namespace GasSystem
{
    public interface IRefuelable
    {
        float CurrentFuel { get; }
        float MaxFuel { get; }

        // YENİ EKLENEN KURAL: Yakıt alan obje duruyor mu?
        bool IsStationary { get; }

        void AddFuel(float amount);
    }
}