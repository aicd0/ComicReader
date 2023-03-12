using System.ComponentModel;
using Windows.UI.Xaml.Media.Imaging;

namespace ComicReader.DesignData
{
    internal class ReaderImageViewModel {

        private BitmapImage _image;
        public BitmapImage Image
        {
            get => _image;
            set
            {
                BitmapImage newValue;
                if (ImageSet)
                {
                    newValue = value;
                }
                else
                {
                    newValue = null;
                }
                if (_image != newValue)
                {
                    _image = newValue;
                    if (newValue == null)
                    {
                        ImageSet = false;
                    }
                }
            }
        }

        private bool _imageSet = false;
        public bool ImageSet
        {
            get => _imageSet;
            set
            {
                _imageSet = value;
                if (!value)
                {
                    Image = null;
                }
            }
        }
    }
}
