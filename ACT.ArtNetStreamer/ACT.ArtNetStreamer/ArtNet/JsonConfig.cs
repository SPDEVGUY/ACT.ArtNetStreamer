using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ACT.ArtNetStreamer.ArtNet
{
    public class JsonConfig
    {
        public class Universe
        {
            public List<Position> Points;
        }
        public class Position
        {
            public int X;
            public int Y;
        }

        public List<Universe> Universes { get; set; }
        public int SquareWidth { get; set; }
        public int SquareHeight { get; set; }

        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }

        public bool EnableDimmerChannel { get; set; }

        public static JsonConfig Load() {
            if(!File.Exists("config.json"))
            {

                DisplayUsage();

                var defaultCfg = GetDefault();
                defaultCfg.Save();
            }
            var contents = File.ReadAllText("config.json")??string.Empty;
            return JsonConvert.DeserializeObject<JsonConfig>(contents) ?? GetDefault();
        }

        private static void DisplayUsage()
        {
            MessageBox.Show(@"
Hey!
This thing makes an ArtNet device you can pipe RGB values to.

Mouse:
You can resize the window by dragging the edge.
Double click the window to remove the frame.
[Left] button to click and drag boxes and view IDs. Press Delete to delete the device you are dragging.
[Middle] adds device to a universe.
[Right] save configuration file.

Resize squares:
Up/Down arrows.

Dimmer:
Press Space Bar to enable/disable Dimmer Channel.
You must add the dimmer channel to your artnet output for devices which is 4 bytes total (RGBD).

Background/Template:
Replace template.png next to the EXE to use a template.

This was made by Alexi from www.AmplifiedCode.ca
");
        }

        public void Save()
        {
            File.WriteAllText("config.json", JsonConvert.SerializeObject(this));
        }

        public static JsonConfig GetDefault()
        {
            var pts1 = new List<Position>();
            var pts2 = new List<Position>();
            for (var x = 0; x < 5; x++)
            {
                pts1.Add(new Position { X = 32 * x, Y = 0 });
                pts2.Add(new Position { X = 32 * x, Y = 33 });
            }


            return new JsonConfig
            {
                SquareHeight = 32,
                SquareWidth = 32,
                WindowHeight = 32*5,
                WindowWidth = 32*5,
                Universes = new List<Universe>
                {
                    new Universe { Points=pts1},
                    new Universe { Points=pts2}
                }            
            };
        }
    }
}
