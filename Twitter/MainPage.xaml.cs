using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Twitter
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public TwitterWinRT.TwitterWinRT TwitterWinRT { get; private set; }

        public MainPage()
        {
            this.InitializeComponent();

            this.TwitterWinRT = new TwitterWinRT.TwitterWinRT("YouConsumerKey", "YouConsumerSecret", "http://google.fr");
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {

        }

        private async void AuthenticationButton_Click_1(object sender, RoutedEventArgs e)
        {
            await TwitterWinRT.GainAccessToTwitter();
        }

        private async void AuthenticationButton_Click_2(object sender, RoutedEventArgs e)
        {
            if (this.TwitterWinRT.AccessGranted)
            {
                if (await TwitterWinRT.UpdateStatus("test"))
                {
                    // SUCCESS!
                }
            }
            else
                new MessageDialog("You need to obtain access before!").ShowAsync();
        }

        private async void AuthenticationButton_Click_3(object sender, RoutedEventArgs e)
        {
            if (this.TwitterWinRT.AccessGranted)
            {
                var list = await TwitterWinRT.GetTimeline();
                if (list.Any())
                {
                    // SUCCESS!
                }
            }
            else
                new MessageDialog("You need to obtain access before!").ShowAsync();
        }
    }
}
