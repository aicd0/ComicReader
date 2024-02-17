using ComicReader.Common;
using ComicReader.Common.Router;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views
{
    sealed internal partial class HelpPage : NavigatablePage
    {
        public HelpPage()
        {
            InitializeComponent();
        }

        public override void OnStart(NavigationParams p)
        {
            base.OnStart(p);
            p.TabId.Tab.Header = Utils.StringResourceProvider.GetResourceString("Help");
            p.TabId.Tab.IconSource = new SymbolIconSource() { Symbol = Symbol.Help };
        }

        public override void OnResume()
        {
            base.OnResume();
        }

        public override void OnPause()
        {
            base.OnPause();
        }

        public override void OnSelected()
        {
        }
    }
}
