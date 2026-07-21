namespace PlotNRots.SaveSystem
{
    public interface ISaveable
    {
        // Kendi verini paketle ve gönder
        object SaveState();

        // Sana gelen paketi al ve kendi üzerine uygula
        void LoadState(object state);
    }
}