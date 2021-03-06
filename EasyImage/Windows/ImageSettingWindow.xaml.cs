﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EasyImage.Actioins;
using EasyImage.Controls;
using EasyImage.Enum;
using EasyImage.UnmanagedToolkit;

namespace EasyImage.Windows
{
    /// <summary>
    /// ImageSettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ImageSettingWindow
    {
        private readonly ImageControl _imageControl;
        private readonly RotateTransform _rotateTransform;
        private readonly TranslateTransform _translateTransform;
        private readonly ScaleTransform _scaleTransform;
        private double _originWidth, _originHeight, _oldWidth, _oldHeight, _oldAngle, _oldTranslateX, _oldTranslateY;
        private bool _oldIsLockAspect;
        private bool _selectedPixelRbn;
        private bool _textChanged;
        private bool _canTextChange;
        private TextboxFlag _textboxFlag;
        private bool _isModified;
        private const double Factor = 0.01;

        public SetPropertyAction SetPropertyAction { get; private set; }

        public ImageSettingWindow(ImageControl imageControl)
        {
            InitializeComponent();
            _imageControl = imageControl;
            _rotateTransform = imageControl.GetTransform<RotateTransform>();
            _translateTransform = imageControl.GetTransform<TranslateTransform>();
            _scaleTransform = imageControl.GetTransform<ScaleTransform>();
            _selectedPixelRbn = true;
            _textChanged = false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.RemoveSystemMenuItems(Win32.SystemMenuItems.All); //去除窗口指定的系统菜单
            var image = (BitmapImage)((AnimatedImage.AnimatedGif)(_imageControl.Content)).Source;
            _originWidth = image.Width;
            _originHeight = image.Height;
            _oldWidth = Math.Round(_imageControl.Width, 2);
            _oldHeight = Math.Round(_imageControl.Height, 2);
            _oldAngle = _rotateTransform.Angle;
            _oldTranslateX = Math.Round(_translateTransform.X, 2);
            _oldTranslateY = Math.Round(_translateTransform.Y, 2);
            _oldIsLockAspect = _imageControl.IsLockAspect;

            OriginHeightValueLbl.Content = Math.Round(_originHeight, 2);
            OriginWidthValueLbl.Content = Math.Round(_originWidth, 2);
            HeightTbx.Text = _oldHeight.ToString(CultureInfo.InvariantCulture);
            WidthTbx.Text = _oldWidth.ToString(CultureInfo.InvariantCulture);
            AngleTbx.Text = _oldAngle.ToString(CultureInfo.InvariantCulture);
            LocationXTbx.Text = _oldTranslateX.ToString(CultureInfo.InvariantCulture);
            LocationYTbx.Text = _oldTranslateY.ToString(CultureInfo.InvariantCulture);
            CheckBox.IsChecked = _oldIsLockAspect;
            _canTextChange = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isModified && _oldIsLockAspect == _imageControl.IsLockAspect) return;
            SetPropertyAction = new SetPropertyAction(_imageControl,
                _oldWidth, _oldHeight, _oldAngle, _oldIsLockAspect, _oldTranslateX, _oldTranslateY,
                _imageControl.Width, _imageControl.Height, _rotateTransform.Angle, _imageControl.IsLockAspect, _translateTransform.X, _translateTransform.Y
                );
        }

        private void DragMoveWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void PixelRbn_Checked(object sender, RoutedEventArgs e)
        {
            _selectedPixelRbn = true;
            if (_imageControl == null) return;
            _canTextChange = false;

            HeightTbx.Text = Math.Round(_imageControl.Height, 2).ToString(CultureInfo.InvariantCulture);
            WidthTbx.Text = Math.Round(_imageControl.Width, 2).ToString(CultureInfo.InvariantCulture);

            _canTextChange = true;
        }

        private void PixelRbn_Unchecked(object sender, RoutedEventArgs e)
        {
            _selectedPixelRbn = false;
            if(_imageControl == null)return;
            _canTextChange = false;

            var height = Math.Round(_imageControl.Height/_originHeight*100, 2);
            var width = Math.Round(_imageControl.Width/_originWidth*100, 2);
            HeightTbx.Text = height.ToString(CultureInfo.InvariantCulture);
            WidthTbx.Text = width.ToString(CultureInfo.InvariantCulture);

            _canTextChange = true;
        }

        private void LeftBtn_Click(object sender, RoutedEventArgs e)
        {
            Reset();
        }

        private void RightBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Reset();
            Close();
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            _imageControl.IsLockAspect = CheckBox.IsChecked.GetValueOrDefault();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(!_canTextChange) return;
            var textbox = sender as TextBox;
            if (textbox == null) return;
            _textboxFlag = (TextboxFlag)textbox.Tag;
            _textChanged = true;
        }

        private void TextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!_textChanged) return;
            SetValue();
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_textChanged) return;
            SetValue();
        }

        private void SetHeightValue()
        {
            double height, newWidth = 0;
            if (double.TryParse(HeightTbx.Text, out height))
            {
                height = Math.Round(height, 2);
                if (height > 0)
                {
                    if (_selectedPixelRbn)
                    {
                        if (Math.Abs(_imageControl.Height - height) >= Factor)
                        {
                            if (_imageControl.IsLockAspect)
                            {
                                newWidth = Math.Round(height / _imageControl.Height * _imageControl.Width, 2);
                            }
                            _imageControl.Height = height;
                            _isModified = true;
                        }
                    }
                    else
                    {
                        height = Math.Round(height/100*_originHeight, 2);
                        if (height > 0 && Math.Abs(_imageControl.Height - height) >= Factor)
                        {
                            if (_imageControl.IsLockAspect)
                            {
                                newWidth = Math.Round(height / _imageControl.Height * _imageControl.Width, 2);
                            }
                            _imageControl.Height = height;
                            _isModified = true;
                           
                        }
                    }
                }
            }
          
            _canTextChange = false;
            HeightTbx.Text = _selectedPixelRbn ? Math.Round(_imageControl.Height, 2).ToString(CultureInfo.InvariantCulture) : Math.Round(_imageControl.Height / _originHeight * 100, 2).ToString(CultureInfo.InvariantCulture);
            if (newWidth > 0)
            {
                //同步修改宽度
                _imageControl.Width = newWidth;
                WidthTbx.Text = _selectedPixelRbn ? newWidth.ToString(CultureInfo.InvariantCulture) : Math.Round(newWidth / _originWidth * 100, 2).ToString(CultureInfo.InvariantCulture);
            }
            _canTextChange = true;
        }

        private void SetWidthValue()
        {
           
            double width, newHeight = 0;
            if (double.TryParse(WidthTbx.Text, out width))
            {
                width = Math.Round(width, 2);
                if (width > 0)
                {
                    if (_selectedPixelRbn)
                    {
                        if (Math.Abs(_imageControl.Width - width) >= Factor)
                        {
                            if (_imageControl.IsLockAspect)
                            {
                                newHeight = Math.Round(width / _imageControl.Width * _imageControl.Height, 2);
                            }
                            _imageControl.Width = width;
                            _isModified = true;
                        }
                    }
                    else
                    {
                        width = Math.Round(width / 100 * _originWidth, 2);
                        if (width > 0 && Math.Abs(_imageControl.Width - width) >= Factor)
                        {
                            if (_imageControl.IsLockAspect)
                            {
                                newHeight = Math.Round(width / _imageControl.Width * _imageControl.Height, 2);
                            }
                            _imageControl.Width = width;
                            _isModified = true;
                        }
                    }
                }
            }
            _canTextChange = false;
            WidthTbx.Text = _selectedPixelRbn ? Math.Round(_imageControl.Width, 2).ToString(CultureInfo.InvariantCulture) : Math.Round(_imageControl.Width / _originWidth * 100, 2).ToString(CultureInfo.InvariantCulture);
            if (newHeight > 0)
            {
                //同步修改高度
                _imageControl.Height = newHeight;
                HeightTbx.Text = _selectedPixelRbn ? newHeight.ToString(CultureInfo.InvariantCulture) : Math.Round(newHeight / _originHeight * 100, 2).ToString(CultureInfo.InvariantCulture);
            }
            _canTextChange = true;
        }

        private void SetAngleValue()
        {
            double angle;
            ReviseRotateCenter();//重置旋转中心
            if (double.TryParse(AngleTbx.Text, out angle))
            {
                angle = Math.Round(angle) % 360;
                if (angle < 0)
                {
                    angle += 360;
                }
                if (Math.Abs(_rotateTransform.Angle - angle) >= Factor)
                {
                    _rotateTransform.Angle = angle;
                    _isModified = true;
                }
            }
            _canTextChange = false;
            AngleTbx.Text = _rotateTransform.Angle.ToString(CultureInfo.InvariantCulture);
            _canTextChange = true;
        }

        private void SetTranslateXValue()
        {
            double translateX;
            if (double.TryParse(LocationXTbx.Text, out translateX))
            {
                translateX = Math.Round(translateX, 2);
                if (Math.Abs(_translateTransform.X - translateX) >= Factor)
                {
                    _translateTransform.X = translateX;
                    _isModified = true;
                }
            }
            _canTextChange = false;
            LocationXTbx.Text = Math.Round(_translateTransform.X, 2).ToString(CultureInfo.InvariantCulture);
            _canTextChange = true;

        }

        private void SetTranslateYValue()
        {
            double translateY;
            if (double.TryParse(LocationYTbx.Text, out translateY))
            {
                translateY = Math.Round(translateY, 2);
                if (Math.Abs(_translateTransform.Y - translateY) >= Factor)
                {
                    _translateTransform.Y = translateY;
                    _isModified = true;
                }
            }
            _canTextChange = false;
            LocationYTbx.Text = Math.Round(_translateTransform.Y, 2).ToString(CultureInfo.InvariantCulture);
            _canTextChange = true;

        }

        /// <summary>
        /// 校正旋转中心
        /// </summary>
        private void ReviseRotateCenter()
        {
            var center = _imageControl.TranslatePoint(new Point(_imageControl.Width / 2, _imageControl.Height / 2), null);
            var centerX = _imageControl.Width*_scaleTransform.ScaleX/2;
            var centerY = _imageControl.Height*_scaleTransform.ScaleY/2;
            _rotateTransform.CenterX = centerX;
            _rotateTransform.CenterY = centerY;
            _translateTransform.X = center.X - centerX;
            _translateTransform.Y = center.Y - centerY;
        }

        private void SetValue()
        {
            try
            {
                switch (_textboxFlag)
                {
                    case TextboxFlag.First:
                        SetHeightValue();
                        break;
                    case TextboxFlag.Second:
                        SetWidthValue();
                        break;
                    case TextboxFlag.Third:
                        SetAngleValue();
                        break;
                    case TextboxFlag.Forth:
                        SetTranslateXValue();
                        break;
                    case TextboxFlag.Fifth:
                        SetTranslateYValue();
                        break;
                }
                _textChanged = false;
            }
            catch (Exception ex)
            {
                App.Log.Error(ex.ToString());
                Extentions.ShowMessageBox("无效的输入!");
            }
        }

        private void Reset()
        {
            _canTextChange = false;

            _imageControl.Height = _oldHeight;
            _imageControl.Width = _oldWidth;
            _rotateTransform.Angle = _oldAngle;
            _translateTransform.X = _oldTranslateX;
            _translateTransform.Y = _oldTranslateY;
            _imageControl.IsLockAspect = _oldIsLockAspect;

            HeightTbx.Text = _oldHeight.ToString(CultureInfo.InvariantCulture);
            WidthTbx.Text = _oldWidth.ToString(CultureInfo.InvariantCulture);
            AngleTbx.Text = _oldAngle.ToString(CultureInfo.InvariantCulture);
            LocationXTbx.Text = _oldTranslateX.ToString(CultureInfo.InvariantCulture);
            LocationYTbx.Text = _oldTranslateY.ToString(CultureInfo.InvariantCulture);
            CheckBox.IsChecked = _oldIsLockAspect;

            PixelRbn.IsChecked = true;
            _isModified = false;

            _canTextChange = true;
        }
    }
}
