using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ComicReader.DesignData;

namespace ComicReader.Controls
{
    public sealed partial class ComicItemHorizontal : UserControl
    {
        public ComicItemViewModel Ctx => DataContext as ComicItemViewModel;

        public ComicItemHorizontal()
        {
            InitializeComponent();
            DataContextChanged += (s, e) => Bindings.Update();
        }

        public bool IsContextFlyoutEnabled
        {
            get { return (bool)GetValue(IsContextFlyoutEnabledProperty); }
            set { SetValue(IsContextFlyoutEnabledProperty, value); }
        }
        public static readonly DependencyProperty IsContextFlyoutEnabledProperty =
            DependencyProperty.Register(nameof(IsContextFlyoutEnabled), typeof(bool), typeof(ComicItemHorizontal), new PropertyMetadata(true));

        private void OnMenuFlyoutOpening(object sender, object e)
        {
            if (!IsContextFlyoutEnabled)
            {
                (sender as MenuFlyout).Hide();
            }
        }

        private void OnUserControlTapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            // prevent tap events being dispatched to other controls
            e.Handled = true;
        }
    }
}
