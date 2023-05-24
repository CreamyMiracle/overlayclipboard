using GameOverlay.Drawing;
using GameOverlay.Windows;
using Microsoft.VisualBasic.Devices;
using SharpDX.Direct2D1;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace OverlayClipboard
{
    public class Overlay : IDisposable
    {
        private GameOverlay.Drawing.SolidBrush _backgroundBrush;
        private GameOverlay.Drawing.SolidBrush _blackBrush;
        private GameOverlay.Drawing.SolidBrush _whiteBrush;
        private GameOverlay.Drawing.Font _font;
        private GameOverlay.Drawing.Font _fontItalic;
        private readonly GraphicsWindow _window;
        private KeyValuePair<string, byte[]> _currImg = new KeyValuePair<string, byte[]>();
        private readonly float _opacity = 0.35f;

        public Overlay()
        {

            var gfx = new GameOverlay.Drawing.Graphics()
            {
                MeasureFPS = true,
                PerPrimitiveAntiAliasing = true,
                TextAntiAliasing = true
            };

            int totalWidth = Screen.AllScreens.ToList().Sum(screen => screen.Bounds.Width);
            int maxHeight = Screen.AllScreens.ToList().MaxBy(screen => screen.Bounds.Height).Bounds.Height;

            _window = new GraphicsWindow(0, 0, totalWidth, maxHeight, gfx)
            {
                FPS = 40,
                IsTopmost = true,
                IsVisible = true
            };

            _window.DestroyGraphics += _window_DestroyGraphics;
            _window.DrawGraphics += _window_DrawGraphics;
            _window.SetupGraphics += _window_SetupGraphics;
        }

        private enum ClipboardContentType
        {
            None,
            Text,
            TextNonPasteable,
            Image
        }

        private KeyValuePair<ClipboardContentType, object>? GetClipboard()
        {
            try
            {
                KeyValuePair<ClipboardContentType, object>? clipboardObj = new KeyValuePair<ClipboardContentType, object>();
                Thread staThread = new Thread(delegate ()
                    {
                        try
                        {
                            if (Clipboard.ContainsText())
                            {
                                clipboardObj = new KeyValuePair<ClipboardContentType, object>(ClipboardContentType.Text, Clipboard.GetText());
                            }
                            else if (Clipboard.ContainsImage())
                            {
                                clipboardObj = new KeyValuePair<ClipboardContentType, object>(ClipboardContentType.Image, Clipboard.GetImage());
                            }
                            else if (Clipboard.ContainsFileDropList())
                            {
                                clipboardObj = new KeyValuePair<ClipboardContentType, object>(ClipboardContentType.TextNonPasteable, string.Join(Environment.NewLine, Clipboard.GetFileDropList().Cast<string>()));
                            }
                            else if (Clipboard.ContainsAudio())
                            {
                                Stream juu = Clipboard.GetAudioStream();
                                clipboardObj = new KeyValuePair<ClipboardContentType, object>(ClipboardContentType.TextNonPasteable, "Audio");
                            }
                        }
                        catch (Exception ex)
                        {
                        }
                    });
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
                staThread.Join();
                return clipboardObj;
            }
            catch (Exception exception)
            {
                return null;
            }
        }

        private void _window_SetupGraphics(object sender, SetupGraphicsEventArgs e)
        {
            var gfx = e.Graphics;

            _backgroundBrush = gfx.CreateSolidBrush(0, 0, 0, 0);
            _blackBrush = gfx.CreateSolidBrush(0, 0, 0, _opacity);
            _whiteBrush = gfx.CreateSolidBrush(255, 255, 255, _opacity);

            _font = gfx.CreateFont("Segoe UI", 12);
            _fontItalic = gfx.CreateFont("Segoe UI", 12, italic: true);
        }

        private void _window_DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
        {
        }

        private void _window_DrawGraphics(object sender, DrawGraphicsEventArgs e)
        {
            DateTime now = DateTime.UtcNow;

            Win32API.POINT p = new Win32API.POINT();
            bool success = Win32API.GetCursorPos(out p);
            int clipBoardOverlayStartX = p.X + 20;
            int clipBoardOverlayStartY = p.Y - 10;

            // clipboard
            KeyValuePair<ClipboardContentType, object>? clipboardObj = GetClipboard();
            GameOverlay.Drawing.SolidBrush mousePixelBrush = FontColorBasedOnBackground(GetPixelColor(clipBoardOverlayStartX, clipBoardOverlayStartY));

            var gfx = e.Graphics;
            gfx.ClearScene(_backgroundBrush);


            if (clipboardObj.HasValue && clipboardObj.Value.Key != ClipboardContentType.None)
            {
                if (clipboardObj.Value.Key == ClipboardContentType.Text)
                {
                    gfx.DrawText(_font, mousePixelBrush, clipBoardOverlayStartX, clipBoardOverlayStartY, (string)clipboardObj.Value.Value);
                }
                else if (clipboardObj.Value.Key == ClipboardContentType.TextNonPasteable)
                {
                    gfx.DrawText(_fontItalic, mousePixelBrush, clipBoardOverlayStartX, clipBoardOverlayStartY, (string)clipboardObj.Value.Value);
                }
                else if (clipboardObj.Value.Key == ClipboardContentType.Image)
                {
                    System.Drawing.Image ogImg = (System.Drawing.Image)clipboardObj.Value.Value;
                    if (ogImg == null)
                    {
                        clipboardObj = new KeyValuePair<ClipboardContentType, object>();
                        return;
                    }

                    Size size = ogImg.Size;
                    string imgId = size.ToString() +
                        ogImg.Flags.ToString() +
                        ogImg.PixelFormat.ToString() +
                        ogImg.RawFormat.ToString() +
                        ogImg.VerticalResolution.ToString() +
                        ogImg.HorizontalResolution.ToString();

                    ogImg = new System.Drawing.Bitmap(ogImg, new Size(Convert.ToInt32(size.Width * 0.1), Convert.ToInt32(size.Height * 0.1)));

                    if (string.IsNullOrEmpty(_currImg.Key) || _currImg.Key != imgId)
                    {
                        using (var ms = new MemoryStream())
                        {
                            ogImg.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                            var bytes = ms.ToArray();
                            _currImg = new KeyValuePair<string, byte[]>(imgId, bytes);

                            GameOverlay.Drawing.Image overlayImg = new GameOverlay.Drawing.Image(gfx, bytes);
                            gfx.DrawImage(overlayImg, clipBoardOverlayStartX, clipBoardOverlayStartY, _opacity);
                        }
                    }
                    else if (_currImg.Key == imgId)
                    {
                        var bytes = _currImg.Value;
                        GameOverlay.Drawing.Image overlayImg = new GameOverlay.Drawing.Image(gfx, bytes);
                        gfx.DrawImage(overlayImg, clipBoardOverlayStartX, clipBoardOverlayStartY, _opacity);
                    }
                }
            }

        }

        private GameOverlay.Drawing.SolidBrush FontColorBasedOnBackground(System.Drawing.Color bg)
        {
            if (bg.R * 2 + bg.G * 7 + bg.B < 500)
                return _whiteBrush;
            else
                return _blackBrush;
        }

        static private System.Drawing.Color GetPixelColor(int x, int y)
        {
            IntPtr hdc = Win32API.GetDC(IntPtr.Zero);
            uint pixel = Win32API.GetPixel(hdc, x, y);
            Win32API.ReleaseDC(IntPtr.Zero, hdc);
            System.Drawing.Color color = System.Drawing.Color.FromArgb((int)(pixel & 0x000000FF), (int)(pixel & 0x0000FF00) >> 8, (int)(pixel & 0x00FF0000) >> 16);
            return color;
        }



        public void Run()
        {
            _window.Create();
            _window.Join();
        }

        ~Overlay()
        {
            Dispose(false);
        }

        #region IDisposable Support
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                _window.Dispose();

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
