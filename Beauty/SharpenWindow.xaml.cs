﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IPlugins;
using Brushes = System.Windows.Media.Brushes;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Beauty
{
    /// <summary>
    /// SharpenWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SharpenWindow : IDisposable
    {
        private readonly Bitmap _cacheFirstBitmap;
        private readonly IEnumerable<Bitmap> _cacheBitmaps;
        private Bitmap _resultBitmap;
        private WriteableBitmap _writeableBitmap;
        private byte[] _bitmapBuffer;
        private double _factor;

        public HandleResult HandleResult { get; private set; }

        public SharpenWindow(IEnumerable<Bitmap> bitmaps)
        {
            InitializeComponent();
            _cacheBitmaps = bitmaps;
            _cacheFirstBitmap = _cacheBitmaps.First();

            var screenHeight = SystemParameters.VirtualScreenHeight;
            var screenWidth = SystemParameters.VirtualScreenWidth;
            var height = _cacheFirstBitmap.Height + 125.0;
            var width = _cacheFirstBitmap.Width + 40.0;
            if (height < 300)
            {
                height = 300;
            }
            else if (height > screenHeight)
            {
                height = screenHeight;
            }
            if (width < 300)
            {
                width = 300;
            }
            else if (width > screenWidth)
            {
                width = screenWidth;
            }
            Height = height;
            Width = width;
            _factor = 0.5;
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            this.RemoveSystemMenuItems(Win32.SystemMenuItems.All); //去除窗口指定的系统菜单
            TitleLbl.Content = $"锐化处理: {_factor * 100}";
            _resultBitmap = GetHandledImage(_cacheFirstBitmap, _factor);
            _writeableBitmap = new WriteableBitmap(Imaging.CreateBitmapSourceFromHBitmap(_resultBitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()));
            TargetImage.Source = _writeableBitmap;
        }

        private void WindowKeyDown(object sender, KeyEventArgs e)
        {
            var value = (int)Slider.Value;
            if (e.Key == Key.Left)
            {
                if (value >= 1)
                {
                    Slider.Value = value - 1;
                }
            }
            else if (e.Key == Key.Right)
            {
                if (value <= 999)
                {
                    Slider.Value = value + 1;
                }
            }
        }

        private void DragMoveWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void LeftBtn_Click(object sender, RoutedEventArgs e)
        {
            HandleResult = new HandleResult(null, false);
            Close();
        }

        private void RightBtn_Click(object sender, RoutedEventArgs e)
        {
            var resultBitmaps = new List<Bitmap>()
            {
                (Bitmap)_resultBitmap.Clone()
            };
            for (var i = 1; i < _cacheBitmaps.Count(); i++)
            {
                resultBitmaps.Add(GetHandledImage(_cacheBitmaps.ElementAt(i), _factor));
            }
            HandleResult = new HandleResult(resultBitmaps, true);
            Close();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            HandleResult = new HandleResult(null, false);
            Close();
        }

        private void ExchangeBgCbx_Click(object sender, RoutedEventArgs e)
        {
            if (ExchangeBgCbx.IsChecked == true)
            {
                ImageViewGrid.Background = Brushes.White;
                ImageBorder.BorderThickness = new Thickness(1);
            }
            else
            {
                ImageViewGrid.Background = Brushes.Transparent;
                ImageBorder.BorderThickness = new Thickness(0);
            }
        }

        private void ExchangeBgCbx_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dialog = new ColorDialog();
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var color = dialog.Color;
            ImageViewGrid.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_cacheFirstBitmap == null) return;
            _resultBitmap?.Dispose();
            var newValue = (int)e.NewValue;
            TitleLbl.Content = $"锐化处理: {newValue}";
            _factor = newValue/100.0;
            _resultBitmap = GetHandledImage(_cacheFirstBitmap, _factor);
            UpdateImage(_resultBitmap);
        }

        private Bitmap GetHandledImage(Bitmap bitmap, double factor)
        {
            var bmp = (Bitmap)bitmap.Clone();
            try
            {
                var width = bmp.Width;
                var height = bmp.Height;
                const int pixelSize = 4;

                var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                var byColorInfo = new byte[height * bmpData.Stride];
                Marshal.Copy(bmpData.Scan0, byColorInfo, 0, byColorInfo.Length);

                #region Safe

                //var clone = (byte[])byColorInfo.Clone();
                //for (var x = 1; x < width - 1; x++)
                //{
                //    for (var y = 1; y < height - 1; y++)
                //    {
                //        var index = y * bmpData.Stride + x * pixelSize;
                //        var byB = clone[index] + (clone[index] - (clone[index - bmpData.Stride - pixelSize] + clone[index - bmpData.Stride] + clone[index - bmpData.Stride + pixelSize] + clone[index - pixelSize] + clone[index + pixelSize] + clone[index + bmpData.Stride - pixelSize] + clone[index + bmpData.Stride] + clone[index + bmpData.Stride + pixelSize]) / 8.0) * factor;
                //        var byG = clone[index + 1] + (clone[index + 1] - (clone[index - bmpData.Stride - pixelSize + 1] + clone[index - bmpData.Stride + 1] + clone[index - bmpData.Stride + pixelSize + 1] + clone[index - pixelSize + 1] + clone[index + pixelSize + 1] + clone[index + bmpData.Stride - pixelSize + 1] + clone[index + bmpData.Stride + 1] + clone[index + bmpData.Stride + pixelSize + 1]) / 8.0) * factor;
                //        var byR = clone[index + 2] + (clone[index + 2] - (clone[index - bmpData.Stride - pixelSize + 2] + clone[index - bmpData.Stride + 2] + clone[index - bmpData.Stride + pixelSize + 2] + clone[index - pixelSize + 2] + clone[index + pixelSize + 2] + clone[index + bmpData.Stride - pixelSize + 2] + clone[index + bmpData.Stride + 2] + clone[index + bmpData.Stride + pixelSize + 2]) / 8.0) * factor;
                //        if (byB > 255) byB = 255;
                //        else if (byB < 0) byB = 0;
                //        if (byG > 255) byG = 255;
                //        else if (byG < 0) byG = 0;
                //        if (byR > 255) byR = 255;
                //        else if (byR < 0) byR = 0;
                //        byColorInfo[index] = (byte)byB;
                //        byColorInfo[index + 1] = (byte)byG;
                //        byColorInfo[index + 2] = (byte)byR;
                //    }
                //}
                //Marshal.Copy(byColorInfo, 0, bmpData.Scan0, byColorInfo.Length);


                #endregion

                #region Unsafe

                unsafe
                {
                    fixed (byte* source = byColorInfo)
                    {
                        var ptr = (byte*)(bmpData.Scan0);
                        for (var y = 1; y < height - 1; y++)
                        {
                            for (var x = 1; x < width - 1; x++)
                            {
                                var index = y * bmpData.Stride + x * pixelSize;
                                var byB = source[index] + (source[index] - (source[index - bmpData.Stride - pixelSize] + source[index - bmpData.Stride] + source[index - bmpData.Stride + pixelSize] + source[index - pixelSize] + source[index + pixelSize] + source[index + bmpData.Stride - pixelSize] + source[index + bmpData.Stride] + source[index + bmpData.Stride + pixelSize]) / 8.0) * factor;
                                var byG = source[index + 1] + (source[index + 1] - (source[index - bmpData.Stride - pixelSize + 1] + source[index - bmpData.Stride + 1] + source[index - bmpData.Stride + pixelSize + 1] + source[index - pixelSize + 1] + source[index + pixelSize + 1] + source[index + bmpData.Stride - pixelSize + 1] + source[index + bmpData.Stride + 1] + source[index + bmpData.Stride + pixelSize + 1]) / 8.0) * factor;
                                var byR = source[index + 2] + (source[index + 2] - (source[index - bmpData.Stride - pixelSize + 2] + source[index - bmpData.Stride + 2] + source[index - bmpData.Stride + pixelSize + 2] + source[index - pixelSize + 2] + source[index + pixelSize + 2] + source[index + bmpData.Stride - pixelSize + 2] + source[index + bmpData.Stride + 2] + source[index + bmpData.Stride + pixelSize + 2]) / 8.0) * factor;
                                if (byB > 255) byB = 255;
                                else if (byB < 0) byB = 0;
                                if (byG > 255) byG = 255;
                                else if (byG < 0) byG = 0;
                                if (byR > 255) byR = 255;
                                else if (byR < 0) byR = 0;
                                ptr[index] = (byte)byB;
                                ptr[index + 1] = (byte)byG;
                                ptr[index + 2] = (byte)byR;
                            }
                        }
                    }
                }

                #endregion

                bmp.UnlockBits(bmpData);
                return bmp;
            }
            catch (Exception e)
            {
                HandleResult = new HandleResult(e);
                Close();
                return (Bitmap)bitmap.Clone();
            }
            
        }

        private void UpdateImage(Bitmap bitmap)
        {
            _writeableBitmap.Lock();
            var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            if (_bitmapBuffer == null)
            {
                _bitmapBuffer = new byte[bitmap.Height * bmpData.Stride];
            }
            Marshal.Copy(bmpData.Scan0, _bitmapBuffer, 0, _bitmapBuffer.Length);
            Marshal.Copy(_bitmapBuffer, 0, _writeableBitmap.BackBuffer, _bitmapBuffer.Length);
            bitmap.UnlockBits(bmpData);
            _writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, _writeableBitmap.PixelWidth, _writeableBitmap.PixelHeight));
            _writeableBitmap.Unlock();
        }

        public void Dispose()
        {
            _resultBitmap?.Dispose();
        }
    }
}
