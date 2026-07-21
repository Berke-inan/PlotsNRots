using System;
using System.Collections.Generic;

namespace PlotNRots.SaveSystem
{
    [Serializable]
    public class GameData
    {
        // Barkod (string) ve O objenin içindeki verilerin JSON hali (string)
        public Dictionary<string, string> savedEntities = new Dictionary<string, string>();
    }
}