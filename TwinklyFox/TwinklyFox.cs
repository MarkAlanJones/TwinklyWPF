using ExtensionMethods;
using System.Diagnostics;
using System.Drawing; // use drawing instead of meadia for named color ?

namespace Twinkly.Fox
{
    /// <summary>
    /// Arduino FastLED demo - converted to C# for Twinkly networked lights 
    /// From https://gist.github.com/kriegsman/756ea6dcae8e30845b5a
    /// Featured in HAD https://hackaday.com/2022/12/21/led-christmas-lights-optimized-for-max-twinkleage/
    /// Converted to C# my Mark Jones - Dec 23 2022
    /// </summary>
    public class TwinklyFox
    {
        public TwinklyFox(int num_leds, int bpl)
        {
            NUM_LEDS = num_leds;
            bytesperled = bpl;
            Leds = new byte[num_leds * bytesperled];
            gTargetPalette = ChooseNextColorPalette();
            gCurrentPalette = (Color[])gTargetPalette.Clone();

            pstopWatch = new Stopwatch();
            blendsw = new Stopwatch();
        }

        private readonly Stopwatch pstopWatch;
        private readonly Stopwatch blendsw;

        public int NUM_LEDS { get; private set; } = 100;
        private int bytesperled { get; set; } = 3;

        /// <summary>
        /// track how many times through the loop
        /// </summary>
        public int LoopCount { get; private set; } = 0;

        public string CurrentPalette { get; private set; } = string.Empty;

        //  TwinkleFOX: Twinkling 'holiday' lights that fade in and out.
        //  Colors are chosen from a palette; a few palettes are provided.
        //
        //  This December 2015 implementation improves on the December 2014 version
        //  in several ways:
        //  - smoother fading, compatible with any colors and any palettes
        //  - easier control of twinkle speed and twinkle density
        //  - supports an optional 'background color'
        //  - takes even less RAM: zero RAM overhead per pixel
        //  - illustrates a couple of interesting techniques (uh oh...)
        //
        //  The idea behind this (new) implementation is that there's one
        //  basic, repeating pattern that each pixel follows like a waveform:
        //  The brightness rises from 0..255 and then falls back down to 0.
        //  The brightness at any given point in time can be determined as
        //  as a function of time, for example:
        //    brightness = sine( time ); // a sine wave of brightness over time
        //
        //  So the way this implementation works is that every pixel follows
        //  the exact same wave function over time.  In this particular case,
        //  I chose a sawtooth triangle wave (triwave8) rather than a sine wave,
        //  but the idea is the same: brightness = triwave8( time ).  
        //  
        //  Of course, if all the pixels used the exact same wave form, and 
        //  if they all used the exact same 'clock' for their 'time base', all
        //  the pixels would brighten and dim at once -- which does not look
        //  like twinkling at all.
        //
        //  So to achieve random-looking twinkling, each pixel is given a 
        //  slightly different 'clock' signal.  Some of the clocks run faster, 
        //  some run slower, and each 'clock' also has a random offset from zero.
        //  The net result is that the 'clocks' for all the pixels are always out 
        //  of sync from each other, producing a nice random distribution
        //  of twinkles.
        //
        //  The 'clock speed adjustment' and 'time offset' for each pixel
        //  are generated randomly.  One (normal) approach to implementing that
        //  would be to randomly generate the clock parameters for each pixel 
        //  at startup, and store them in some arrays.  However, that consumes
        //  a great deal of precious RAM, and it turns out to be totally
        //  unnessary!  If the random number generate is 'seeded' with the
        //  same starting value every time, it will generate the same sequence
        //  of values every time.  So the clock adjustment parameters for each
        //  pixel are 'stored' in a pseudo-random number generator!  The PRNG 
        //  is reset, and then the first numbers out of it are the clock 
        //  adjustment parameters for the first pixel, the second numbers out
        //  of it are the parameters for the second pixel, and so on.
        //  In this way, we can 'store' a stable sequence of thousands of
        //  random clock adjustment parameters in literally two bytes of RAM.
        //
        //  There's a little bit of fixed-point math involved in applying the
        //  clock speed adjustments, which are expressed in eighths.  Each pixel's
        //  clock speed ranges from 8/8ths of the system clock (i.e. 1x) to
        //  23/8ths of the system clock (i.e. nearly 3x).
        //
        //  On a basic Arduino Uno or Leonardo, this code can twinkle 300+ pixels
        //  smoothly at over 50 updates per seond.
        //
        //  -Mark Kriegsman, December 2015

        // the realtime buffer has 1 FRAME  3 or 4 bytes for every light
        // header bytes are handled in DataAccess
        // This Frame is filled by the Loop Function, once the Loop has run, the frame can be displayed
        public byte[] Leds { get; private set; }

        #region User Tweakable Properties

        // Overall twinkle speed.
        // 0 (VERY slow) to 8 (VERY fast).  
        // 4, 5, and 6 are recommended, default is 4.
        public int TWINKLE_SPEED { get; set; } = 4;

        // Overall twinkle density.
        // 0 (NONE lit) to 8 (ALL lit at once).  
        // Default is 5.
        public int TWINKLE_DENSITY { get; set; } = 5;

        // How often to change color palettes.
        public int SECONDS_PER_PALETTE { get; set; } = 30;
        // Also: toward the bottom of the file is an array 
        // called "ActivePaletteList" which controls which color
        // palettes are used; you can add or remove color palettes
        // from there freely.

        // Background color for 'unlit' pixels
        // Can be set toColor.Black if desired.
        private Color gBackgroundColor = Color.Black;
        private static readonly Color FairyLightNCC = Color.FromArgb(0xFF, 0x9D, 0x2A);

        // Example of dim incandescent fairy light background color
        // CRGB gBackgroundColor = CRGB(CRGB::FairyLight).nscale8_video(16);

        // If AUTO_SELECT_BACKGROUND_COLOR is set to 1,
        // then for any palette where the first two entries 
        // are the same, a dimmed version of that color will
        // automatically be used as the background color.
        public bool AUTO_SELECT_BACKGROUND_COLOR { get; set; } = false;

        // If COOL_LIKE_INCANDESCENT is set to 1, colors will 
        // fade out slighted 'reddened', similar to how
        // incandescent bulbs change color as they get dim down.
        public bool COOL_LIKE_INCANDESCENT { get; set; } = true;

        #endregion

        // Pallette of 16 colors
        private Color[] gCurrentPalette;
        private Color[] gTargetPalette;

        private bool changepalette = false;
        private bool blendpalette = false;

        /// <summary>
        /// Start Here then get property leds
        /// call loop often, properties can be adjusted between loops
        /// </summary>
        public async Task loop()
        {
            if (!pstopWatch.IsRunning)
                pstopWatch.Start();
            if (!blendsw.IsRunning)
                blendsw.Start();

            // change palette
            if (pstopWatch.ElapsedMilliseconds / 1000 >= SECONDS_PER_PALETTE)
                changepalette = true;

            if (changepalette)
            {
                pstopWatch.Restart();
                blendsw.Restart();
                changepalette = false;
                gTargetPalette = ChooseNextColorPalette();
            }

            // blend palette
            if (blendsw.ElapsedMilliseconds >= 10)
                blendpalette = true;

            if (blendpalette)
            {
                blendsw.Restart();
                blendpalette = false;
                gCurrentPalette = NblendPaletteTowardPalette(gCurrentPalette, gTargetPalette, 12);
            }

            await Task.Run(() => DrawTwinkles());

            // Now the leds Frame is ready
            LoopCount++;
        }

        public void Reset()
        {
            Leds = new byte[NUM_LEDS * bytesperled];
            LoopCount = 0;

            pal = -1;
            CurrentPalette = string.Empty;
            gTargetPalette = ChooseNextColorPalette();
            gCurrentPalette = (Color[])gTargetPalette.Clone();

            pstopWatch.Stop();
            pstopWatch.Reset();
            blendsw.Stop();
            blendsw.Reset();
        }

        #region Private Methods

        private static Color[] NblendPaletteTowardPalette(Color[] curr, Color[] targ, int v)
        {
            var outpal = (Color[])curr.Clone();
            byte r, g, b;
            for (var i = 0; i < targ.Length; i++)
            {
                var c1 = outpal[i];
                var c2 = targ[i];

                // blend up or down
                if (c1.ToArgb() > c2.ToArgb())
                {
                    r = (byte)Math.Max(c1.R - v, c2.R);
                    g = (byte)Math.Max(c1.G - v, c2.G);
                    b = (byte)Math.Max(c1.B - v, c2.B);
                }
                else
                {
                    r = (byte)Math.Max(c1.R + v, c2.R);
                    g = (byte)Math.Max(c1.G + v, c2.G);
                    b = (byte)Math.Max(c1.B + v, c2.B);
                }
                outpal[i] = Color.FromArgb(r, g, b);
            }
            return outpal;
        }

        //  This function loops over each pixel, calculates the 
        //  adjusted 'clock' that this pixel should use, and calls 
        //  "CalculateOneTwinkle" on each pixel.  It then displays
        //  either the twinkle color of the background color, 
        //  whichever is brighter.
        private void DrawTwinkles()
        {
            // "PRNG16" is the pseudorandom number generator
            // It MUST be reset to the same starting value each time
            // this function is called, so that the sequence of 'random'
            // numbers that it generates is (paradoxically) stable.
            ushort PRNG16 = 11337;

            uint clock32 = (uint)pstopWatch.ElapsedMilliseconds;

            // Set up the background color, "bg".
            // if AUTO_SELECT_BACKGROUND_COLOR == 1, and the first two colors of
            // the current palette are identical, then a deeply faded version of
            // that color is used for the background color
            Color bg;
            if (AUTO_SELECT_BACKGROUND_COLOR &&
                (gCurrentPalette[0] == gCurrentPalette[1]))
            {
                bg = gCurrentPalette[0];
                byte bglight = (byte)HSBColor.FromColor(bg).B;
                if (bglight > 64)
                {
                    bg.nscale8_video(16); // very bright, so scale to 1/16th
                }
                else if (bglight > 16)
                {
                    bg.nscale8_video(64); // not that bright, so scale to 1/4th
                }
                else
                {
                    bg.nscale8_video(86); // dim, scale to 1/3rd.
                }
            }
            else
            {
                bg = gBackgroundColor; // just use the explicitly defined background color
            }

            byte backgroundBrightness = (byte)HSBColor.FromColor(bg).B;

            for (var i = 0; i < Leds.Length; i = i + bytesperled)
            {
                Color pixel = Color.FromArgb(Leds[i], Leds[i + 1], Leds[i + 2]);

                PRNG16 = (ushort)((PRNG16 * 2053) + 1384); // next 'random' number
                ushort myclockoffset16 = PRNG16; // use that number as clock offset
                PRNG16 = (ushort)((PRNG16 * 2053) + 1384); // next 'random' number
                                                           // use that number as clock speed adjustment factor (in 8ths, from 8/8ths to 23/8ths)
                byte myspeedmultiplierQ5_3 = (byte)(((((PRNG16 & 0xFF) >> 4) + (PRNG16 & 0x0F)) & 0x0F) + 0x08);
                uint myclock30 = (uint)((clock32 * myspeedmultiplierQ5_3) >> 3) + myclockoffset16;
                byte myunique8 = (byte)(PRNG16 >> 8); // get 'salt' value for this pixel

                // We now have the adjusted 'clock' for this pixel, now we call
                // the function that computes what color the pixel should be based
                // on the "brightness = f( time )" idea.
                Color c = ComputeOneTwinkle(myclock30, myunique8);

                byte cbright = (byte)HSBColor.FromColor(c).B;
                short deltabright = (short)(cbright - backgroundBrightness);
                if (deltabright >= 32) // || !bg ??
                {
                    // If the new pixel is significantly brighter than the background color, 
                    // use the new color.
                    pixel = c;
                }
                else if (deltabright > 0)
                {
                    // If the new pixel is just slightly brighter than the background color,
                    // mix a blend of the new color and the background color
                    pixel = Blend(bg, c, deltabright * 8);
                }
                else
                {
                    // if the new pixel is not at all brighter than the background color,
                    // just use the background color.
                    pixel = bg;
                }

                Leds[i] = pixel.R;
                Leds[i + 1] = pixel.G;
                Leds[i + 2] = pixel.B;
            }
        }

        /// <summary>Blends the specified colors together.</summary>
        /// <param name="color">Color to blend onto the background color.</param>
        /// <param name="backColor">Color to blend the other color onto.</param>
        /// <param name="amount">How much of <paramref name="color"/> to keep,
        /// “on top of” <paramref name="backColor"/>.</param>
        /// <returns>The blended colors.</returns>
        private static Color Blend(Color color, Color backColor, double amount)
        {
            byte r = (byte)(color.R * amount + backColor.R * (1 - amount));
            byte g = (byte)(color.G * amount + backColor.G * (1 - amount));
            byte b = (byte)(color.B * amount + backColor.B * (1 - amount));
            return Color.FromArgb(r, g, b);
        }

        //  This function takes a time in pseudo-milliseconds,
        //  figures out brightness = f( time ), and also hue = f( time )
        //  The 'low digits' of the millisecond time are used as 
        //  input to the brightness wave function.  
        //  The 'high digits' are used to select a color, so that the color
        //  does not change over the course of the fade-in, fade-out
        //  of one cycle of the brightness wave function.
        //  The 'high digits' are also used to determine whether this pixel
        //  should light at all during this cycle, based on the TWINKLE_DENSITY.
        private Color ComputeOneTwinkle(uint ms, byte salt)
        {
            ushort ticks = (ushort)(ms >> (8 - TWINKLE_SPEED));
            byte fastcycle8 = (byte)ticks;
            ushort slowcycle16 = (ushort)((ticks >> 8) + salt);
            slowcycle16 += (ushort)(Math.Sin(slowcycle16) * 255);
            slowcycle16 = (ushort)((slowcycle16 * 2053) + 1384);
            byte slowcycle8 = (byte)((slowcycle16 & 0xFF) + (slowcycle16 >> 8));

            byte bright = 0;
            if (((slowcycle8 & 0x0E) / 2) < TWINKLE_DENSITY)
            {
                bright = AttackDecayWave8(fastcycle8);
            }

            byte hue = (byte)(slowcycle8 - salt);
            Color c;
            if (bright > 0)
            {
                c = ColorFromPalette(gCurrentPalette, hue, bright);
                if (COOL_LIKE_INCANDESCENT)
                {
                    CoolLikeIncandescent(c, fastcycle8);
                }
            }
            else
            {
                c = Color.Black;
            }
            return c;
        }

        /// <summary>
        /// get the palette item by index, then brighten iit
        /// Regardless of the number of entries in the base palette, this function will interpolate 
        /// between entries to turn the discrete colors into a smooth gradient.
        /// </summary>
        /// <param name="p">pal the palette to retrieve the color from</param>
        /// <param name="hue">not index color to look for</param>
        /// <param name="bright">brightness optional brightness value to scale the resulting color</param>
        /// <returns>Color</returns>
        private static Color ColorFromPalette(Color[] p, byte hue, byte bright)
        {
            // find palette entry with the closest hue
            var closest = p[0];
            var distance = 255;
            foreach (var c in p)
            {
                var phue = c.GetHue() / 360 * 255;
                if (Math.Abs(hue - phue) < distance)
                {
                    distance = (int)Math.Abs(hue - phue);
                    closest = c;
                }
            }
            var result = closest;
            if (bright > 0)
            {
                result = HSBColor.ShiftBrighness(closest, bright);
            }
            return result;
        }

        // This function is like 'triwave8', which produces a 
        // symmetrical up-and-down triangle sawtooth waveform, except that this
        // function produces a triangle wave with a faster attack and a slower decay:
        //
        //     / \ 
        //    /     \ 
        //   /         \ 
        //  /             \ 
        //

        private static byte AttackDecayWave8(byte i)
        {
            if (i < 86)
            {
                return (byte)(i * 3);
            }
            else
            {
                i -= 86;
                return (byte)(255 - (i + (i / 2)));
            }
        }

        // This function takes a pixel, and if its in the 'fading down'
        // part of the cycle, it adjusts the color a little bit like the 
        // way that incandescent bulbs fade toward 'red' as they dim.
        private static Color CoolLikeIncandescent(Color c, byte phase)
        {
            if (phase < 128) return c;

            byte cooling = (byte)((phase - 128) >> 4);

            var G = c.G;
            if (cooling < G)
                G -= cooling;
            else
                G = 0;

            var B = c.B;
            if ((cooling * 2) < B)
                B -= (byte)(cooling * 2);
            else
                B = 0;

            return Color.FromArgb(c.R, G, B);
        }

        #endregion

        #region Palettes

        // A mostly red palette with green accents and white trim.
        // "CRGB::Gray" is used as white to keep the brightness more uniform.
        private static readonly Color[] RedGreenWhite_p = new Color[]
        {
            Color.Red,Color.Red,Color.Red,Color.Red,
            Color.Red,Color.Red,Color.Red,Color.Red,
            Color.Red,Color.Red,Color.Gray,Color.Gray,
            Color.Green,Color.Green,Color.Green,Color.Green
        };

        // A mostly (dark) green palette with red berries.0c);
        private static readonly Color Holly_Green = Color.FromArgb(0x00580c);
        private static readonly Color Holly_Red = Color.FromArgb(0xB00402);
        private static readonly Color[] Holly_p = new Color[]
        {
            Holly_Green, Holly_Green, Holly_Green, Holly_Green,
            Holly_Green, Holly_Green, Holly_Green, Holly_Green,
            Holly_Green, Holly_Green, Holly_Green, Holly_Green,
            Holly_Green, Holly_Green, Holly_Green, Holly_Red
        };

        // A red and white striped palette
        // "CRGB::Gray" is used as white to keep the brightness more uniform.
        private static readonly Color[] RedWhite_p = new Color[]
        {
            Color.Red,Color.Red,Color.Red,Color.Red,
            Color.Gray,Color.Gray,Color.Gray,Color.Gray,
            Color.Red,Color.Red,Color.Red,Color.Red,
            Color.Gray,Color.Gray,Color.Gray,Color.Gray
        };

        // A mostly blue palette with white accents.
        // "CRGB::Gray" is used as white to keep the brightness more uniform.
        private static readonly Color[] BlueWhite_p = new Color[]
        {
            Color.Blue,Color.Blue,Color.Blue,Color.Blue,
            Color.Blue,Color.Blue,Color.Blue,Color.Blue,
            Color.Blue,Color.Blue,Color.Blue,Color.Blue,
            Color.Blue,Color.Gray,Color.Gray,Color.Gray
        };

        //A pure "fairy light" palette with some brightness variations
        private const int FL = 0xFFE42D;
        private static readonly Color FairyLight = Color.FromArgb(FL);
        private static readonly Color HALFFAIRY = Color.FromArgb((FL & 0xFEFEFE) / 2);
        private static readonly Color QUARTERFAIRY = Color.FromArgb((FL & 0xFCFCFC) / 4);
        private static readonly Color[] FairyLight_p = new Color[]
        {
            FairyLight, FairyLight, FairyLight, FairyLight,
            HALFFAIRY, HALFFAIRY, FairyLight, FairyLight,
            QUARTERFAIRY, QUARTERFAIRY, FairyLight, FairyLight,
            FairyLight, FairyLight, FairyLight, FairyLight
        };

        // A palette of soft snowflakes with the occasional bright one
        private static readonly Color[] Snow_p = new Color[]
        {
            Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0x304048),
            Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0x304048),
            Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0x304048),
            Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0x304048), Color.FromArgb(0xE0F0FF)
        };

        // A palette reminiscent of large 'old-school' C9-size tree lights
        // in the five classic colors: red, orange, green, blue, and white.
        private static readonly Color C9_Red = Color.FromArgb(0xB8, 0x04, 0x00);
        private static readonly Color C9_Orange = Color.FromArgb(0x90, 0x2C, 0x02);
        private static readonly Color C9_Green = Color.FromArgb(0x04, 0x60, 0x02);
        private static readonly Color C9_Blue = Color.FromArgb(0x07, 0x07, 0x58);
        private static readonly Color C9_White = Color.FromArgb(0x6, 0x68, 0x20);
        private static readonly Color[] RetroC9_p = new Color[]
        {
            C9_Red, C9_Orange, C9_Red, C9_Orange,
            C9_Orange, C9_Red, C9_Orange, C9_Red,
            C9_Green, C9_Green, C9_Green, C9_Green,
            C9_Blue, C9_Blue, C9_Blue, C9_White
        };

        // A cold, icy pale blue palette
        private static readonly Color Ice_Blue1 = Color.FromArgb(0x0C, 0x10, 0x40);
        private static readonly Color Ice_Blue2 = Color.FromArgb(0x18, 0x20, 0x80);
        private static readonly Color Ice_Blue3 = Color.FromArgb(0x50, 0x80, 0xC0);
        private static readonly Color[] Ice_p = new Color[]
        {
            Ice_Blue1, Ice_Blue1, Ice_Blue1, Ice_Blue1,
            Ice_Blue1, Ice_Blue1, Ice_Blue1, Ice_Blue1,
            Ice_Blue1, Ice_Blue1, Ice_Blue1, Ice_Blue1,
            Ice_Blue2, Ice_Blue2, Ice_Blue2, Ice_Blue3
        };

        // From FastLED
        private static readonly Color[] RainbowColors_p = new Color[]
        {
            Color.FromArgb(0xFF0000), Color.FromArgb(0xD52A00), Color.FromArgb(0xAB5500), Color.FromArgb(0xAB7F00),
            Color.FromArgb(0xABAB00), Color.FromArgb(0x56D500), Color.FromArgb(0x00FF00), Color.FromArgb(0x00D52A),
            Color.FromArgb(0x00AB55), Color.FromArgb(0x0056AA), Color.FromArgb(0x0000FF), Color.FromArgb(0x2A00D5),
            Color.FromArgb(0x5500AB), Color.FromArgb(0x7F0081), Color.FromArgb(0xAB0055), Color.FromArgb(0xD5002B)
        };

        // From FastLED
        private static readonly Color[] PartyColors_p = new Color[]
        {
            Color.FromArgb(0x5500AB), Color.FromArgb(0x84007C), Color.FromArgb(0xB5004B), Color.FromArgb(0xE5001B),
            Color.FromArgb(0xE81700), Color.FromArgb(0xB84700), Color.FromArgb(0xAB7700), Color.FromArgb(0xABAB00),
            Color.FromArgb(0xAB5500), Color.FromArgb(0xDD2200), Color.FromArgb(0xF2000E), Color.FromArgb(0xC2003E),
            Color.FromArgb(0x8F0071), Color.FromArgb(0x5F00A1), Color.FromArgb(0x2F00D0), Color.FromArgb(0x0007F9)
        };

        // Add or remove palette names from this list to control which color
        // palettes are used, and in what order.
        private static List<Color[]> ActivePaletteList = new List<Color[]>()
        {
            RetroC9_p,
            BlueWhite_p,
            RainbowColors_p,
            FairyLight_p,
            RedGreenWhite_p,
            PartyColors_p,
            RedWhite_p,
            Snow_p,
            Holly_p,
            Ice_p
        };

        private static List<string> ActivePaletteNamesList = new List<string>()
        {
            "Retro C9",
            "Blue White",
            "Rainbow Colors",
            "Fairy Light",
            "Red Green White",
            "Party Colors",
            "Red White",
            "Snow",
            "Holly",
            "Ice"
        };

        private int pal = -1;
        // Advance to the next color palette in the list (above).
        private Color[] ChooseNextColorPalette()
        {
            pal += 1;
            if (pal >= ActivePaletteList.Count)
                pal = 0;

            CurrentPalette = ActivePaletteNamesList[pal];
            return ActivePaletteList[pal];
        }

        #endregion

    }
}