using System.Configuration;
using System.Data;
using System.Windows;
using SpriteEditor.Helpers;

namespace SpriteEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Initialize global error handler
            GlobalErrorHandler.Initialize();
        }

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

        public static string GetStr(string key, params object[] args)
        {
            if (Application.Current.Resources.Contains(key))
            {
                string text = Application.Current.Resources[key] as string;
                if (args.Length > 0)
                {
                    return string.Format(text, args);
                }
                return text;
            }
            return $"[{key}]"; // Əgər tapılmasa, key-i qaytarır ki, xəbərimiz olsun
        }
    }

}
