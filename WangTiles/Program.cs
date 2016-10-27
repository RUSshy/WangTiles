﻿using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System.Drawing;
using Lunar.Utils;
using System.Net;

namespace WangTiles
{
    public class Example
    {
        #region TEXTURE_BUFFER_UTILS
        private static int bufferWidth = 600;
        private static int bufferHeight = 400;
        private static byte[] buffer = new byte[bufferWidth * bufferHeight * 4];

        static void UpdateBuffer(byte[] buffer, int width, int height, int texID)
        {
            GL.BindTexture(TextureTarget.Texture2D, texID);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, buffer);
        }

        public static void DrawBuffer(OpenTK.GameWindow game, int textureID)
        {
            float u1 = 0;
            float u2 = 1;
            float v1 = 0;
            float v2 = 1;

            float w = 1;
            float h = 1;


            float px = 0;
            float py = 0;

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0.0, 1.0, 0.0, 1.0, 0.0, 4.0);

            GL.Enable(EnableCap.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, textureID);

            GL.Begin(PrimitiveType.Quads);

            GL.Color3(Color.White);
            GL.TexCoord2(u1, v1);
            GL.Vertex2(px + 0, py + h);

            GL.TexCoord2(u1, v2);
            GL.Vertex2(px + 0, py + 0);

            GL.TexCoord2(u2, v2);
            GL.Vertex2(px + w, py + 0);

            GL.TexCoord2(u2, v1);
            GL.Vertex2(px + w, py + h);

            GL.End();
        }
        #endregion

        #region TILESET
        private const int tileSize = 32;

        /// <summary>
        /// Draws a tile in the texture buffer at specified position
        /// </summary>
        private static void DrawTile(int targetX, int targetY, int tileID, Bitmap tileset, Color borderColor)
        {
            for (int y = 0; y < tileSize; y++)
            {
                for (int x = 0; x < tileSize; x++)
                {
                    if (x+targetX>=bufferWidth || x+targetX<0) { continue; }
                    if (y + targetY >= bufferHeight || y+targetY<0) { continue; }
                    int destOfs = ((x + targetX) + bufferWidth * (y + targetY)) * 4;
                    var c = tileset.GetPixel((tileID * tileSize) + x, y);

                    if (borderColor.A>0 && ( x==0 || y== 0 || x == tileSize - 1 || y == tileSize - 1))
                    {
                        c = borderColor;
                    }

                    buffer[destOfs + 0] = c.R;
                    buffer[destOfs + 1] = c.G;
                    buffer[destOfs + 2] = c.B;
                    buffer[destOfs + 3] = c.A;
                }
            }

        }
        #endregion


        private static Random rnd = new Random(3424);

        private static Color GetAreaColor(WangArea area)
        {
            if (area == null)
            {
                return Color.FromArgb(0);
            }

            return area.GetColor();
        }

        private static void RedrawWithTileset(Bitmap tileset)
        {
            for (int j = 0; j < mapHeight; j++)
            {
                for (int i = 0; i < mapWidth; i++)
                {
                    int ofs = GetMapOffset(i, j);
                    int tileID = map[ofs].tileID;
                    if (tileID < 0) { continue; }
                    DrawTile(i * tileSize, j * tileSize, tileID, tileset, GetAreaColor(map[ofs].areaID));
                }
            }
        }

        private static void DownloadTileset(string name)
        {
            using (var client = new WebClient())
            {
                Bitmap target = new Bitmap(512, 32);
                for (int i = 0; i < 16; i++)
                {
                    var url = "http://s358455341.websitehome.co.uk/stagecast/art/edge/" + name + "/" + i + ".gif";
                    var tempName = "temp" + i + ".gif";
                    client.DownloadFile(url, tempName);
                    Bitmap temp = new Bitmap(tempName);
                    Graphics g = Graphics.FromImage(target);
                    g.DrawImage(temp, i * tileSize, 0);
                }
                target.Save("tileset.png");
            }
        }

        private static void FloodFillArea(int x, int y, WangArea areaID)
        {
            int ofs = GetMapOffset(x, y);
            map[ofs].areaID = areaID;

            // flood all adjacent tiles if possible
            FloodFillArea(x - 1, y, areaID, WangDirection.East);
            FloodFillArea(x + 1, y, areaID, WangDirection.West);
            FloodFillArea(x, y - 1, areaID, WangDirection.South);
            FloodFillArea(x, y + 1, areaID, WangDirection.North);
        }

        private static void FloodFillArea(int x, int y, WangArea areaID, WangDirection direction)
        {
            if (x<0 || y<0 || x>=mapWidth || y>=mapHeight) // if out of bounds, return
            {
                return;
            }

            int ofs = GetMapOffset(x, y);
            if (map[ofs].areaID != null) // if already filled then stop
            {
                return;
            }

            if (map[ofs].tileID == 0) // empty tiles should not be filled
            {
                return;
            }

            if (WangUtils.GetConnectionForTile(map[ofs].tileID, direction) == false)
            {
                return;
            }

            FloodFillArea(x, y, areaID);
        }

        private static bool JoinArea(WangArea parent, WangArea area, HashSet<WangArea> areaSet)
        {
            if  (areaSet.Contains(area))
            {
                return false;
            }

            areaSet.Add(area);

            Console.WriteLine("Testing area: " + GetAreaColor(area));

            var result = FindTilesInAreaEdges(area);
            if (result.Count <= 0)
            {
                return true;
            }

            foreach (var temp in result)
            {
                if (JoinArea(area, temp.Key, areaSet))
                {
                    area.children.Add(temp.Key);

                    var tiles = temp.Value;
                    var tile = tiles[rnd.Next(tiles.Count)];

                    Console.WriteLine("Joined " + GetAreaColor(area) + " to " + GetAreaColor(temp.Key));

                    if (GetAreaColor(temp.Key).Equals(Color.Yellow))
                    {
                        area.ID += 0;
                    }

                    int ofs = GetMapOffset(tile.x1, tile.y1);
                    map[ofs].tileID = WangUtils.AddConnection(map[ofs].tileID, tile.exit);

                    ofs = GetMapOffset(tile.x2, tile.y2);
                    map[ofs].tileID = WangUtils.AddConnection(map[ofs].tileID, WangUtils.InvertDirection(tile.exit));
                }
            }

            return true;
        }

        public static void Main(string[] args)
        {
            //DownloadTileset("walkway");

            List<Bitmap> tilesets = new List<Bitmap>();
            // load tilesets
            for (int i=1; i<=9; i++)
            {
                var tileset = new Bitmap("../data/tileset"+i+".png");
                tilesets.Add(tileset);
            }
            
            // first pass clears all tiles
            for (int j = 0; j < mapHeight; j++)
            {
                for (int i = 0; i < mapWidth; i++)
                {
                    int ofs = GetMapOffset(i, j);
                    map[ofs].areaID = null;
                    map[ofs].tileID = -1;
                }
            }
            
            // second pass places random tiles in a checkerboard pattern
            for (int j = 0; j < mapHeight; j++)
            {
                for (int i = 0; i < mapWidth; i++)
                {
                    if (i % 2 == j % 2)
                    {
                        continue;
                    }

                    TryPlacingRandomTile(i, j);
                }
            }

            // third pass places random tiles in the mising holes, checking for matching edges
            for (int j = 0; j < mapHeight; j++)
            {
                for (int i = 0; i < mapWidth; i++)
                {
                    int current = GetMapAt(i, j).tileID;
                    if (current >= 0) // if tile already set, skip
                    {
                        continue;
                    }

                    //continue;

                    TryPlacingRandomTile(i, j);
                }
            }

            // optional pass, invert bits 
            if (mapInvert)
            {
                for (int j = 0; j < mapHeight; j++)
                {
                    for (int i = 0; i < mapWidth; i++)
                    {
                        int current = GetMapAt(i, j).tileID;
                        if (current < 0) // if tile is not set, skip
                        {
                            continue;
                        }

                        SetMapAt(i, j, 15 - current);
                    }
                }
            }

            // optional, detect isolate areas
            List<WangArea> areas = new List<WangArea>(); 
            for (int j = 0; j < mapHeight; j++)
            {
                for (int i = 0; i < mapWidth; i++)
                {
                    int ofs = GetMapOffset(i, j);
                    if (map[ofs].areaID != null) // check if this tile already have an area assigned
                    {
                        continue; 
                    }

                    if (map[ofs].tileID == 0) // empty tiles should not be filled
                    {
                        continue;
                    }

                    var area = new WangArea();
                    area.ID = areas.Count;
                    areas.Add(area);
                    FloodFillArea(i, j, area);
                }
            }

            // join the isolated areas by adding exits to some tiles
            HashSet<WangArea> areaSet = new HashSet<WangArea>();
            for (int k=0;  k<areas.Count; k++)
            {
                JoinArea(null, areas[k], areaSet);
                break;
            }

            // now render the map to a pixel array
            RedrawWithTileset(tilesets[1]);


            int bufferTexID = 0;

            using (var game = new OpenTK.GameWindow(bufferWidth, bufferHeight))
            {
                game.Load += (sender, e) =>
                {
                    // setup settings, load textures, sounds
                    game.VSync = OpenTK.VSyncMode.Off;

                    // generate a texture and copy the pixel array there
                    bufferTexID = GL.GenTexture();
                    UpdateBuffer(buffer, bufferWidth, bufferHeight, bufferTexID);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                };

                game.Resize += (sender, e) =>
                {
                    GL.Viewport(0, 0, game.Width, game.Height);
                };

                game.UpdateFrame += (sender, e) =>
                {
                    if (game.Keyboard[Key.Escape])
                    {
                        game.Exit();
                        Environment.Exit(0);
                    }

                    if (game.Keyboard[Key.Number1])
                    {
                        RedrawWithTileset(tilesets[0]);
                        UpdateBuffer(buffer, bufferWidth, bufferHeight, bufferTexID);
                    }
                    if (game.Keyboard[Key.Number2])
                    {
                        RedrawWithTileset(tilesets[1]);
                        UpdateBuffer(buffer, bufferWidth, bufferHeight, bufferTexID);
                    }
                    if (game.Keyboard[Key.Number3])
                    {
                        RedrawWithTileset(tilesets[2]);
                        UpdateBuffer(buffer, bufferWidth, bufferHeight, bufferTexID);
                    }
                    if (game.Keyboard[Key.Number4])
                    {
                        RedrawWithTileset(tilesets[3]);
                        UpdateBuffer(buffer, bufferWidth, bufferHeight, bufferTexID);
                    }
                    if (game.Keyboard[Key.Number5])
                    {
                        RedrawWithTileset(tilesets[4]);
                        UpdateBuffer(buffer, bufferWidth, bufferHeight, bufferTexID);
                    }
                    if (game.Keyboard[Key.Number6])
                    {
                        RedrawWithTileset(tilesets[5]);
                        UpdateBuffer(buffer, bufferWidth, bufferHeight, bufferTexID);
                    }
                    if (game.Keyboard[Key.Number7])
                    {
                        RedrawWithTileset(tilesets[6]);
                        UpdateBuffer(buffer, bufferWidth, bufferHeight, bufferTexID);
                    }
                    if (game.Keyboard[Key.Number8])
                    {
                        RedrawWithTileset(tilesets[7]);
                        UpdateBuffer(buffer, bufferWidth, bufferHeight, bufferTexID);
                    }
                    if (game.Keyboard[Key.Number9])
                    {
                        RedrawWithTileset(tilesets[8]);
                        UpdateBuffer(buffer, bufferWidth, bufferHeight, bufferTexID);
                    }
                };


                game.RenderFrame += (sender, e) =>
                {
                    // draw the texture to the screen, stretched to fill the whole window
                    DrawBuffer(game, bufferTexID);
                    game.SwapBuffers();
                };

                game.Run();
            }
        }

    }
}
