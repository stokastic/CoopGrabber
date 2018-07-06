
namespace DeluxeGrabber {
    public class ModConfig {
        public int GrabberRange;
        public bool DoGlobalForage;
        public int GlobalForageTileX;
        public int GlobalForageTileY;
        public string GlobalForageMap;

        public ModConfig() {
            DoGlobalForage = true;
            GrabberRange = 10;
            GlobalForageTileX = 6;
            GlobalForageTileY = 35;
            GlobalForageMap = "Desert";
        }
    }
}
