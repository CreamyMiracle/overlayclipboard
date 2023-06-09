﻿using GameOverlay.Drawing;
using GameOverlay.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.ApplicationServices;
using Microsoft.VisualBasic.Devices;
using OverlayClipboard.Hooks;
using OverlayClipboard.Model;
using Refit;
using SharpDX.Direct2D1;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static OverlayClipboard.Win32API;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace OverlayClipboard
{
    internal class Overlay : IDisposable
    {
        private readonly IConfiguration _config;
        private GameOverlay.Drawing.SolidBrush _backgroundBrush;
        private GameOverlay.Drawing.SolidBrush _blackBrush;
        private GameOverlay.Drawing.SolidBrush _whiteBrush;
        private GameOverlay.Drawing.SolidBrush _leftDragAreaBrush;
        private GameOverlay.Drawing.SolidBrush _rightDragAreaBrush;
        private GameOverlay.Drawing.Font _font;
        private GameOverlay.Drawing.Font _fontItalic;
        private readonly GraphicsWindow _window;
        private KeyValuePair<string, byte[]> _currImg = new KeyValuePair<string, byte[]>();
        private readonly float _clipboardOpacity;
        private readonly float _dragAreaOpacity;
        private readonly int _fontColorThreshold;
        private readonly int _left;
        private readonly int _top;
        private readonly int _fps;
        private readonly string _fontName;
        private readonly int _fontSize;
        private readonly bool _followMouse;
        private readonly DisplayMode _displayMode;
        private static object _captureLock = new object();
        private static bool _ocrRunning;
        private static float _currRotAngle = 0f;
        private readonly IOCRAPI _ocrApi;

        private enum DisplayMode
        {
            None,
            Content,
            ContentType
        }

        public Overlay(IConfiguration config)
        {
            _ocrApi = RestService.For<IOCRAPI>("https://freeocrapi.com");

            _config = config;

            Enum.TryParse<DisplayMode>(config["Clipboard:DisplayMode"], out _displayMode);
            _followMouse = bool.Parse(config["Clipboard:RelativeToMouse"]);
            _left = int.Parse(_config["Clipboard:OverlayLeft"]);
            _top = int.Parse(_config["Clipboard:OverlayTop"]);
            _clipboardOpacity = float.Parse(_config["Clipboard:Opacity"]);
            _fontColorThreshold = int.Parse(_config["Clipboard:FontColorThreshold"]);
            _fontSize = int.Parse(_config["Clipboard:FontSize"]);
            _fontName = _config["Clipboard:FontName"];

            _dragAreaOpacity = float.Parse(_config["ScanArea:Opacity"]);


            _fps = int.Parse(_config["OverlayFPS"]);

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
                FPS = _fps,
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
                                if (_displayMode == DisplayMode.ContentType)
                                {
                                    clipboardObj = new KeyValuePair<ClipboardContentType, object>(ClipboardContentType.Text, "Text");
                                }
                                else if (_displayMode == DisplayMode.Content)
                                {
                                    clipboardObj = new KeyValuePair<ClipboardContentType, object>(ClipboardContentType.Text, Clipboard.GetText());
                                }
                            }
                            else if (Clipboard.ContainsImage())
                            {
                                if (_displayMode == DisplayMode.ContentType)
                                {
                                    clipboardObj = new KeyValuePair<ClipboardContentType, object>(ClipboardContentType.Text, "Image");
                                }
                                else if (_displayMode == DisplayMode.Content)
                                {
                                    clipboardObj = new KeyValuePair<ClipboardContentType, object>(ClipboardContentType.Image, Clipboard.GetImage());
                                }
                            }
                            else if (Clipboard.ContainsFileDropList())
                            {
                                if (_displayMode == DisplayMode.ContentType)
                                {
                                    clipboardObj = new KeyValuePair<ClipboardContentType, object>(ClipboardContentType.Text, "FileDropList");
                                }
                                else if (_displayMode == DisplayMode.Content)
                                {
                                    clipboardObj = new KeyValuePair<ClipboardContentType, object>(ClipboardContentType.TextNonPasteable, string.Join(Environment.NewLine, Clipboard.GetFileDropList().Cast<string>()));
                                }
                            }
                            else if (Clipboard.ContainsAudio())
                            {
                                if (_displayMode == DisplayMode.ContentType)
                                {
                                    clipboardObj = new KeyValuePair<ClipboardContentType, object>(ClipboardContentType.Text, "Audio");
                                }
                                else if (_displayMode == DisplayMode.Content)
                                {
                                    Stream juu = Clipboard.GetAudioStream();
                                    clipboardObj = new KeyValuePair<ClipboardContentType, object>(ClipboardContentType.TextNonPasteable, "Audio");
                                }
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

        private bool SetClipboard(KeyValuePair<ClipboardContentType, object> clipboardObj)
        {
            try
            {
                Thread staThread = new Thread(delegate ()
                {
                    try
                    {
                        if (clipboardObj.Key == ClipboardContentType.Text)
                        {
                            Clipboard.SetText((string)clipboardObj.Value);
                        }
                        else if (clipboardObj.Key == ClipboardContentType.Image)
                        {
                            Clipboard.SetImage((System.Drawing.Image)clipboardObj.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                });
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
                staThread.Join();
                return true;
            }
            catch (Exception exception)
            {
                return false;
            }
        }

        private void _window_SetupGraphics(object sender, SetupGraphicsEventArgs e)
        {
            var gfx = e.Graphics;

            _backgroundBrush = gfx.CreateSolidBrush(0, 0, 0, 0);
            _blackBrush = gfx.CreateSolidBrush(0, 0, 0, _clipboardOpacity);
            _whiteBrush = gfx.CreateSolidBrush(255, 255, 255, _clipboardOpacity);

            _leftDragAreaBrush = gfx.CreateSolidBrush(255, 255, 255, _dragAreaOpacity);
            _rightDragAreaBrush = gfx.CreateSolidBrush(255, 255, 255, _dragAreaOpacity);

            _font = gfx.CreateFont(_fontName, _fontSize);
            _fontItalic = gfx.CreateFont(_fontName, _fontSize, italic: true);
        }

        private void _window_DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
        {
        }

        private void _window_DrawGraphics(object sender, DrawGraphicsEventArgs e)
        {
            Win32API.CURSORPOS currPos = new Win32API.CURSORPOS();
            bool success = Win32API.GetCursorPos(out currPos);

            var gfx = e.Graphics;
            gfx.ClearScene(_backgroundBrush);

            if (success)
            {
                lock (_captureLock)
                {
                    DrawSpinner(gfx, currPos);
                    DrawClipboard(gfx, currPos);
                    DrawRightDragArea(gfx, currPos);
                    DrawLeftDragArea(gfx, currPos);
                }
            }
        }

        private void DrawRightDragArea(GameOverlay.Drawing.Graphics? gfx, CURSORPOS currPos)
        {
            if (rightDragStart != null)
            {
                bool currXSmaller = currPos.X < rightDragStart.X;
                bool currYSmaller = currPos.Y < rightDragStart.Y;

                int rectLeft = currXSmaller ? currPos.X : rightDragStart.X;
                int rectTop = currYSmaller ? currPos.Y : rightDragStart.Y;
                int rectRight = !currXSmaller ? currPos.X : rightDragStart.X;
                int rectBot = !currYSmaller ? currPos.Y : rightDragStart.Y;

                gfx?.DashedRectangle(_rightDragAreaBrush, new GameOverlay.Drawing.Rectangle(rectLeft, rectTop, rectRight, rectBot), 1.0f);
                gfx?.DrawText(_font, _leftDragAreaBrush, rectLeft, rectTop - _fontSize - 5, "OCR");
            }
        }

        private void DrawLeftDragArea(GameOverlay.Drawing.Graphics? gfx, CURSORPOS currPos)
        {
            if (leftDragStart != null)
            {
                bool currXSmaller = currPos.X < leftDragStart.X;
                bool currYSmaller = currPos.Y < leftDragStart.Y;

                int rectLeft = currXSmaller ? currPos.X : leftDragStart.X;
                int rectTop = currYSmaller ? currPos.Y : leftDragStart.Y;
                int rectRight = !currXSmaller ? currPos.X : leftDragStart.X;
                int rectBot = !currYSmaller ? currPos.Y : leftDragStart.Y;

                gfx?.DashedRectangle(_leftDragAreaBrush, new GameOverlay.Drawing.Rectangle(rectLeft, rectTop, rectRight, rectBot), 1.0f);
                gfx?.DrawText(_font, _leftDragAreaBrush, rectLeft, rectTop - _fontSize - 5, "Image");
            }
        }

        public interface IOCRAPI
        {
            [Multipart]
            [Post("/api")]
            Task<ApiResponse<OCRResponse>> UploadImage([AliasAs("file")] StreamPart streamPart);

            [Get("/api/get/{uid}")]
            Task<ApiResponse<object>> GetText(string uid);
        }

        private void DrawSpinner(GameOverlay.Drawing.Graphics? gfx, CURSORPOS currPos)
        {
            if (_ocrRunning)
            {
                _currRotAngle += 0.1f;
                GameOverlay.Drawing.Point point = new GameOverlay.Drawing.Point(currPos.X, currPos.Y);
                TransformationMatrix transformation = new TransformationMatrix();
                gfx?.TransformStart(TransformationMatrix.Rotation(_currRotAngle, point));
                gfx?.DashedCircle(_leftDragAreaBrush, new Circle(point, 10f), 2f);
                gfx?.TransformEnd();
            }
        }

        private void DrawClipboard(GameOverlay.Drawing.Graphics? gfx, CURSORPOS currPos)
        {
            if (!_followMouse)
            {
                currPos = new Win32API.CURSORPOS();
            }

            int clipBoardOverlayStartX = currPos.X + _left;
            int clipBoardOverlayStartY = currPos.Y + _top;

            // clipboard
            KeyValuePair<ClipboardContentType, object>? clipboardObj = GetClipboard();
            GameOverlay.Drawing.SolidBrush mousePixelBrush = FontColorBasedOnBackground(GetPixelColor(clipBoardOverlayStartX, clipBoardOverlayStartY));

            if (clipboardObj.HasValue && clipboardObj.Value.Key != ClipboardContentType.None)
            {
                if (clipboardObj.Value.Key == ClipboardContentType.Text)
                {
                    gfx?.DrawText(_font, mousePixelBrush, clipBoardOverlayStartX, clipBoardOverlayStartY, (string)clipboardObj.Value.Value);
                }
                else if (clipboardObj.Value.Key == ClipboardContentType.TextNonPasteable)
                {
                    gfx?.DrawText(_fontItalic, mousePixelBrush, clipBoardOverlayStartX, clipBoardOverlayStartY, (string)clipboardObj.Value.Value);
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

                    //ogImg = new System.Drawing.Bitmap(ogImg, new Size(Convert.ToInt32(size.Width * 0.1), Convert.ToInt32(size.Height * 0.1)));

                    if (string.IsNullOrEmpty(_currImg.Key) || _currImg.Key != imgId)
                    {
                        using (var ms = new MemoryStream())
                        {
                            ogImg.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                            var bytes = ms.ToArray();
                            _currImg = new KeyValuePair<string, byte[]>(imgId, bytes);

                            GameOverlay.Drawing.Image overlayImg = new GameOverlay.Drawing.Image(gfx, bytes);
                            gfx?.DrawImage(overlayImg, clipBoardOverlayStartX, clipBoardOverlayStartY, _clipboardOpacity);
                        }
                    }
                    else if (_currImg.Key == imgId)
                    {
                        var bytes = _currImg.Value;
                        GameOverlay.Drawing.Image overlayImg = new GameOverlay.Drawing.Image(gfx, bytes);
                        gfx?.DrawImage(overlayImg, clipBoardOverlayStartX, clipBoardOverlayStartY, _clipboardOpacity);
                    }
                }
            }
        }

        private GameOverlay.Drawing.SolidBrush FontColorBasedOnBackground(System.Drawing.Color bg)
        {
            int threshold = bg.R * 2 + bg.G * 7 + bg.B;
            if (threshold < _fontColorThreshold)
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
            AttachEventHooks();

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

        #region Event Stuff
        private static MouseHook? _mh;

        private static KeyboardHook? _kh;

        private void AttachEventHooks()
        {
            _mh = new MouseHook();
            _mh.SetHook();
            _mh.MouseEvent += mh_MouseEvent;

            _kh = new KeyboardHook();
            _kh.KeyboardEvent += kh_KeyboardEvent;
            _kh.Start();
        }

        private async void mh_MouseEvent(object sender, MouseEvent me)
        {
            bool keysDown = AreKeysDown(Constants.DefaultAreaScanDownKeys);
            if (keysDown)
            {
                if (me.Type == MouseEventFlag.LeftDown)
                {
                    rightDragStart = null;
                    leftDragStart = me;
                }
                if (me.Type == MouseEventFlag.LeftUp)
                {
                    rightDragEnd = null;
                    leftDragEnd = me;
                }
                if (me.Type == MouseEventFlag.RightDown)
                {
                    leftDragStart = null;
                    rightDragStart = me;
                }
                if (me.Type == MouseEventFlag.RightUp)
                {
                    leftDragEnd = null;
                    rightDragEnd = me;
                }

                KeyValuePair<MouseEvent, MouseEvent>? rightDragCompleted = ValidRightDragCompleted();
                KeyValuePair<MouseEvent, MouseEvent>? leftDragCompleted = ValidLeftDragCompleted();

                if (rightDragCompleted != null)
                {
                    bool currXSmaller = rightDragCompleted.Value.Value.X < rightDragCompleted.Value.Key.X;
                    bool currYSmaller = rightDragCompleted.Value.Value.Y < rightDragCompleted.Value.Key.Y;

                    int rectLeft = currXSmaller ? rightDragCompleted.Value.Value.X : rightDragCompleted.Value.Key.X;
                    int rectTop = currYSmaller ? rightDragCompleted.Value.Value.Y : rightDragCompleted.Value.Key.Y;
                    int rectRight = !currXSmaller ? rightDragCompleted.Value.Value.X : rightDragCompleted.Value.Key.X;
                    int rectBot = !currYSmaller ? rightDragCompleted.Value.Value.Y : rightDragCompleted.Value.Key.Y;

                    System.Drawing.Image img;
                    lock (_captureLock)
                    {
                        _window.Hide();
                        int width = rectRight - rectLeft;
                        int height = rectBot - rectTop;
                        if (width == 0 || height == 0)
                        {
                            return;
                        }
                        img = ScreenCapture.Capture(new System.Drawing.Rectangle(rectLeft, rectTop, width, height));
                        _window.Show();
                    }

                    using (var ms = new MemoryStream())
                    {
                        img.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                        _ocrRunning = true;
                        var res = await _ocrApi.UploadImage(new StreamPart(new MemoryStream(ms.ToArray()), "ocrImg.bmp", "image/bmp"));
                        _ocrRunning = false;
                        if (res.IsSuccessStatusCode)
                        {
                            KeyValuePair<ClipboardContentType, object> clipboardObj = new KeyValuePair<ClipboardContentType, object>(ClipboardContentType.Text, res.Content.Text);
                            SetClipboard(clipboardObj);
                        }
                    }
                }

                if (leftDragCompleted != null)
                {
                    bool currXSmaller = leftDragCompleted.Value.Value.X < leftDragCompleted.Value.Key.X;
                    bool currYSmaller = leftDragCompleted.Value.Value.Y < leftDragCompleted.Value.Key.Y;

                    int rectLeft = currXSmaller ? leftDragCompleted.Value.Value.X : leftDragCompleted.Value.Key.X;
                    int rectTop = currYSmaller ? leftDragCompleted.Value.Value.Y : leftDragCompleted.Value.Key.Y;
                    int rectRight = !currXSmaller ? leftDragCompleted.Value.Value.X : leftDragCompleted.Value.Key.X;
                    int rectBot = !currYSmaller ? leftDragCompleted.Value.Value.Y : leftDragCompleted.Value.Key.Y;

                    System.Drawing.Image img;
                    lock (_captureLock)
                    {
                        _window.Hide();
                        int width = rectRight - rectLeft;
                        int height = rectBot - rectTop;
                        if (width == 0 || height == 0)
                        {
                            return;
                        }
                        img = ScreenCapture.Capture(new System.Drawing.Rectangle(rectLeft, rectTop, width, height));
                        _window.Show();
                    }
                    KeyValuePair<ClipboardContentType, object> clipboardObj = new KeyValuePair<ClipboardContentType, object>(ClipboardContentType.Image, img);
                    SetClipboard(clipboardObj);
                }

            }
            else
            {
                rightDragStart = null;
                rightDragEnd = null;
                leftDragStart = null;
                leftDragEnd = null;
            }
        }

        private KeyValuePair<MouseEvent, MouseEvent>? ValidLeftDragCompleted()
        {
            KeyValuePair<MouseEvent, MouseEvent>? completedEvents = null;
            if (leftDragStart != null && leftDragEnd != null && leftDragEnd.Timestamp > leftDragStart.Timestamp)
            {
                completedEvents = new KeyValuePair<MouseEvent, MouseEvent>(new MouseEvent(leftDragStart), new MouseEvent(leftDragEnd));

                leftDragStart = null;
                leftDragEnd = null;
            }

            return completedEvents;
        }

        private KeyValuePair<MouseEvent, MouseEvent>? ValidRightDragCompleted()
        {
            KeyValuePair<MouseEvent, MouseEvent>? completedEvents = null;
            if (rightDragStart != null && rightDragEnd != null && rightDragEnd.Timestamp > rightDragStart.Timestamp)
            {
                completedEvents = new KeyValuePair<MouseEvent, MouseEvent>(new MouseEvent(rightDragStart), new MouseEvent(rightDragEnd));

                rightDragStart = null;
                rightDragEnd = null;
            }

            return completedEvents;
        }

        private static void kh_KeyboardEvent(object sender, KeyboardEvent ke)
        {
        }

        private static bool AreKeysDown(List<int> vkCodes)
        {
            foreach (int quitVk in vkCodes)
            {
                if (!((Win32API.GetKeyState(quitVk) & 0x80) == 0x80))
                {
                    return false;
                }
            }
            return true;
        }

        private MouseEvent leftDragStart = null;
        private MouseEvent leftDragEnd = null;
        private MouseEvent rightDragStart = null;
        private MouseEvent rightDragEnd = null;
        #endregion
    }
}