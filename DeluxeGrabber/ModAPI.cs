using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace DeluxeGrabber {
    public class PrismaticAPI {
        private readonly ModConfig Config;

        public int GrabberRange { get { return this.Config.GrabberRange; } }

        public IEnumerable<Vector2> GetGrabberCoverage(Vector2 origin) {
            for (int x = -GrabberRange; x <= GrabberRange; x++) {
                for (int y = -GrabberRange; y <= GrabberRange; y++) {
                    yield return new Vector2(x, y) + origin;
                }
            }
        }

        internal PrismaticAPI(ModConfig config) {
            this.Config = config;
        }
    }
}
