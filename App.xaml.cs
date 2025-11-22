using System.Configuration;
using System.Data;
using System.Windows;

namespace SpriteEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public void ChangeLanguage(string cultureCode)
        {
            // Dil faylının yolunu təyin edirik
            var dictionaryUri = new Uri($"Resources/Languages/Lang.{cultureCode}.xaml", UriKind.Relative);
            var resourceDict = Application.LoadComponent(dictionaryUri) as ResourceDictionary;

            if (resourceDict != null)
            {
                // Köhnə dil faylını tapıb silirik (Lang. ilə başlayanları)
                var oldDict = this.Resources.MergedDictionaries
                                            .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Lang."));

                if (oldDict != null)
                {
                    this.Resources.MergedDictionaries.Remove(oldDict);
                }

                // Yeni dili əlavə edirik
                this.Resources.MergedDictionaries.Add(resourceDict);
            }
        }
    }

}
