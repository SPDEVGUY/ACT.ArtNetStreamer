using ACT.ArtNetStreamer.ArtNet;
using ArtNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ACT.ArtNetStreamer
{
    class Program
    {
        static bool PaintNeeded = false;
        static bool Running = false;
        static DmxController Control;
        static JsonConfig Config;
        static RenderForm DisplayForm;

        static bool IsMouseDown = false;
        static int MouseX = 0;
        static int MouseY = 0;
        static int SelectedUniverse = -1;
        static int SelectedDevice = -1;


        public class RenderForm : Form
        {
            public RenderForm() { DoubleBuffered = true; }
        }

        static void FindSelected()
        {
            SelectedUniverse = -1;
            SelectedDevice = -1;

            var uix = 0;
            foreach(var u in Control.Universes)
            {
                var dix = 0;
                foreach(SimpleDmxLight d in u.Devices)
                {
                    if(MouseX >= d.X && MouseY >= d.Y)
                    {
                        if(MouseX <= d.X+d.W && MouseY <= d.Y + d.H)
                        {
                            SelectedUniverse = uix;
                            SelectedDevice = dix;
                            return;
                        }
                    }
                    dix++;
                }
                uix++;
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            
            Config = JsonConfig.Load();

            ReloadControl();

            _ = UpdateThread();
            OpenForm();

            Control.Dispose();
            Running = false;

        }

        public static void ReloadControl()
        {
            var universes = new List<UniverseDevices>();

            foreach (var u in Config.Universes)
            {
                var devices = new List<DmxDevice>();
                for (var d = 0; d < u.Points.Count; d++)
                {
                    var light = new SimpleDmxLight();
                    light.X = u.Points[d].X;
                    light.Y = u.Points[d].Y;
                    light.H = Config.SquareHeight;
                    light.W = Config.SquareWidth;
                    light.UseDimmer = Config.EnableDimmerChannel;

                    light.OnDataUpdated = (l) =>
                    {
                        var simpLight = (SimpleDmxLight)l;

                        //Console.WriteLine("{0}:: R[{1}] G[{2}] B[{3}] A[{4}]", l.Name, simpLight.R, simpLight.G, simpLight.B, simpLight.W);
                        PaintNeeded = true;
                    };
                    devices.Add(light);
                }

                universes.Add(new UniverseDevices(devices, universes.Count));
            }

            lock (Config)
            {
                if (Control != null)
                {

                    var old = Control;
                    Control = null;
                    old.Dispose();
                }
                Control = new DmxController(universes);
            }
        }

        public static void OpenForm()
        {
            DisplayForm = new RenderForm();
            var defaultHeight = (int)(Control.Universes.Count * Config.SquareHeight);
            var defaultWidth = 0;

            foreach (var u in Control.Universes)
            {
                var width = (int)(u.Devices.Count * Config.SquareWidth);
                if (width > defaultWidth) defaultWidth = width;
            }
            if (Config.WindowHeight > 0) defaultHeight = Config.WindowHeight;
            if (Config.WindowWidth > 0) defaultWidth = Config.WindowWidth;

            DisplayForm.FormBorderStyle = FormBorderStyle.Sizable;
            DisplayForm.BackColor = Color.Black;
            DisplayForm.ClientSize = new Size(defaultWidth, defaultHeight);
            DisplayForm.Text = "ArtNet Streamer by Amplified Code Technologies";
            DisplayForm.ClientSizeChanged += (x, e) =>
            {
                Config.WindowHeight = DisplayForm.ClientSize.Height;
                Config.WindowWidth = DisplayForm.ClientSize.Width;
            };

            if (File.Exists("template.png"))
            {
                DisplayForm.BackgroundImage = Image.FromFile("template.png");
            }

            DisplayForm.MouseDown += (x, e) =>
            {
                if(e.Button == MouseButtons.Left)
                {
                    IsMouseDown = true;
                    MouseX = e.X;
                    MouseY = e.Y;

                    FindSelected();

                    if(e.Clicks==2)
                    {
                        DisplayForm.FormBorderStyle = DisplayForm.FormBorderStyle == FormBorderStyle.Sizable ? FormBorderStyle.None : FormBorderStyle.Sizable;
                    }
                }
                if(e.Button == MouseButtons.Right)
                {
                    if(MessageBox.Show("Save config?","Save?",MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        Config.Save();
                    }
                }
                if(e.Button == MouseButtons.Middle)
                {
                    if (SelectedUniverse == -1) SelectedUniverse = 0;

                    Config.Universes[SelectedUniverse].Points.Add(
                        new JsonConfig.Position { X = MouseX, Y = MouseY }
                    );
                    ReloadControl();
                }
            };

            DisplayForm.MouseMove += (o, e) =>
            {
                MouseX = e.X;
                MouseY = e.Y;

                if (IsMouseDown && SelectedDevice>=0 && SelectedUniverse>=0)
                {

                     if (MouseX < 0) MouseX = 0;
                     if (MouseY < 0) MouseY = 0;
                     if (MouseX > Config.WindowWidth - Config.SquareWidth)
                     {
                         MouseX = Config.WindowWidth - Config.SquareWidth;
                     }
                     if (MouseY > Config.WindowHeight - Config.SquareHeight)
                     {
                         MouseY = Config.WindowHeight - Config.SquareHeight;
                     }

                     var p = Config.Universes[SelectedUniverse].Points[SelectedDevice];
                     p.X = MouseX;
                     p.Y = MouseY;

                     var d = (SimpleDmxLight)Control.Universes[SelectedUniverse].Devices[SelectedDevice];
                     d.X = MouseX;
                     d.Y = MouseY;
                }
             };

            DisplayForm.KeyPress += (o, e) =>
            {
                if(e.KeyChar == ' ')
                {
                    Config.EnableDimmerChannel = !Config.EnableDimmerChannel;
                    ReloadControl();
                }
                if(e.KeyChar == '*')
                {
                    Config.Universes.Add(
                        new JsonConfig.Universe { 
                            Points = new List<JsonConfig.Position> { 
                                new JsonConfig.Position { X = 0, Y = 0 }
                            } 
                        }
                    );
                    ReloadControl();
                    SelectedUniverse = Config.Universes.Count - 1;
                }

                //TODO: Add key to auto sort devices into rows. with prompt
                //todo: add page up/down to change window sizes by default sizes.

            };

            DisplayForm.KeyDown += (o, e) =>
            {
                if(e.KeyCode == Keys.Up)
                {
                    Config.SquareHeight++;
                    Config.SquareWidth++;
                    ReloadControl();
                }
                if (e.KeyCode == Keys.Down && (Config.SquareHeight > 1 && Config.SquareWidth > 1))
                {
                    Config.SquareHeight--;
                    Config.SquareWidth--;
                    ReloadControl();
                }

                if (e.KeyCode == Keys.Delete && SelectedUniverse >= 0 && SelectedDevice >= 0)
                {
                    Config.Universes[SelectedUniverse].Points.RemoveAt(SelectedDevice);
                    if (Config.Universes[SelectedUniverse].Points.Count == 0)
                        Config.Universes.RemoveAt(SelectedUniverse);

                    SelectedDevice = -1;
                    SelectedUniverse = -1;

                    ReloadControl();
                }
            };

            DisplayForm.MouseUp += (x, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    IsMouseDown = false;
                    MouseX = e.X;
                    MouseY = e.Y;
                }
            };

            DisplayForm.Paint += DisplayForm_Paint;

            Application.EnableVisualStyles();
            Application.Run(DisplayForm);
        }

        private static void DisplayForm_Paint(object sender, PaintEventArgs e)
        {
            System.Drawing.Font drawFont = new System.Drawing.Font("Arial", 11);
            var fontBrush = new SolidBrush(Color.White);
            var selectedPen = new Pen(Color.White, 2);
            var universePen = new Pen(Color.Cyan,2);
            var normalPen = new Pen(Color.DarkGray);

            var uix = 0;
            foreach(var u in Control.Universes)
            {
                var dix = 0;
                foreach(SimpleDmxLight d in u.Devices)
                {
                    var b = Config.EnableDimmerChannel ?
                        new SolidBrush(Color.FromArgb(d.A, d.R, d.G, d.B))
                        : new SolidBrush(Color.FromArgb(d.R, d.G, d.B));
                    var r = new Rectangle(d.X, d.Y, d.W, d.H);
                    e.Graphics.FillRectangle(b, r);

                    if(IsMouseDown)
                    {

                        if(SelectedUniverse == uix && SelectedDevice == dix)
                        {
                            e.Graphics.DrawRectangle(selectedPen, d.X, d.Y, d.W, d.H);
                        } else if (SelectedUniverse == uix)
                        {
                            e.Graphics.DrawRectangle(universePen, d.X, d.Y, d.W, d.H);
                        } else
                        {
                            e.Graphics.DrawRectangle(normalPen, d.X, d.Y, d.W, d.H);
                        }

                        e.Graphics.FillRectangle(new SolidBrush(Color.Black), new Rectangle(d.X, d.Y + d.H, 64, 24));
                        e.Graphics.DrawString($"U{uix}:D{dix}", drawFont, fontBrush, d.X + 2, d.Y + d.H);
                    }

                    dix++;
                }
                uix++;
            }
        }

        public static async Task UpdateThread()
        {
            Running = true;

            var ticks = 0;
            while(Running)
            {
                if(PaintNeeded)
                {
                    lock (Config)
                    {
                        if (DisplayForm != null) DisplayForm.Invalidate();
                        if (ticks > 200)
                        {
                            Console.Clear();
                            foreach (var u in Control.Universes)
                            {
                                Console.WriteLine($"[{u.UniverseName}]");
                                foreach (SimpleDmxLight d in u.Devices)
                                {
                                    if (Config.EnableDimmerChannel)
                                    {
                                        Console.WriteLine($"\t[{d.Name}]:: R[{d.R}] G[{d.G}] B[{d.B}] DIM[{d.A}]");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"\t[{d.Name}]:: R[{d.R}] G[{d.G}] B[{d.B}]");
                                    }

                                }
                            }
                            ticks = 0;
                        }
                    }
                }

                ticks++;
                await Task.Delay(1);
            }
        }
    }
}
