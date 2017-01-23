using EPiServer.Commerce.Order;
using EPiServer.Core;
using EPiServer.Globalization;
using EPiServer.Web;
using Mediachase.Commerce;
using Mediachase.Commerce.Catalog;
using Mediachase.Commerce.Catalog.Managers;
using Mediachase.Commerce.Catalog.Objects;
using Mediachase.Commerce.Orders;
using Mediachase.Commerce.Website;
using Mediachase.Commerce.Website.Helpers;
using System;
using System.Collections;
using System.Threading;
using System.Web;

namespace EPiServer.Business.Commerce.Payment
{
    internal static class Utilities
    {
        private const string CurrentCartKey = "CurrentCart";
        private const string CurrentContextKey = "CurrentContext";

        // Refer to: http://tech.dibspayment.com/D2/Toolbox/Currency_codes
        public const string DKK = "208";
        public const string EUR = "978";
        public const string USD = "840";
        public const string GBP = "826";
        public const string SEK = "752";
        public const string AUD = "036";
        public const string CAD = "124";
        public const string ISK = "352";
        public const string JPY = "392";
        public const string NZD = "554";
        public const string NOK = "578";
        public const string CHF = "756";
        public const string TRY = "949";

        /// <summary>
        /// Get display name with current language
        /// </summary>
        /// <param name="item">The line item of oder</param>
        /// <param name="maxSize">The number of character to get display name</param>
        /// <returns>Display name with current language</returns>
        public static string GetDisplayNameOfCurrentLanguage(this ILineItem item, int maxSize)
        {
            Entry entry = CatalogContext.Current.GetCatalogEntry(item.Code, new CatalogEntryResponseGroup(CatalogEntryResponseGroup.ResponseGroup.CatalogEntryInfo));
            // if the entry is null (product is deleted), return item display name
            return (entry != null) ? 
                        StoreHelper.GetEntryDisplayName(entry).StripPreviewText(maxSize <= 0 ? 100 : maxSize) : 
                        item.DisplayName.StripPreviewText(maxSize <= 0 ? 100 : maxSize);
        }

        /// <summary>
        /// Update display name with current language
        /// </summary>
        /// <param name="po">The purchase order</param>
        public static void UpdateDisplayNameWithCurrentLanguage(this IPurchaseOrder po)
        {
            if (po != null)
            {
                foreach (ILineItem item in po.GetAllLineItems())
                {
                    item.DisplayName = item.GetDisplayNameOfCurrentLanguage(100);
                }
            }
        }
        
        /// <summary>
        /// Uses parameterized thread to update the cart instance id otherwise will get an "workflow already existed" exception.
        /// Passes the cart and the current HttpContext as parameter in call back function to be able to update the instance id and also can update the HttpContext.Current if needed.
        /// </summary>
        /// <param name="cart">The cart to update.</param>
        /// <remarks>
        /// This method is used internal for payment methods which has redirect standard for processing payment e.g: PayPal, DIBS
        /// </remarks>
        internal static void UpdateCartInstanceId(ICart cart)
        {
            ParameterizedThreadStart threadStart = UpdateCartCallbackFunction;
            var thread = new Thread(threadStart);
            var cartInfo = new Hashtable();
            cartInfo[CurrentCartKey] = cart;
            cartInfo[CurrentContextKey] = HttpContext.Current;
            thread.Start(cartInfo);
            thread.Join();
        }

        /// <summary>
        /// Callback function for updating the cart. Before accept all changes of the cart, update the HttpContext.Current if it is null somehow.
        /// </summary>
        /// <param name="cartArgs">The cart arguments for updating.</param>
        private static void UpdateCartCallbackFunction(object cartArgs)
        {
            var cartInfo = cartArgs as Hashtable;
            if (cartInfo == null || !cartInfo.ContainsKey(CurrentCartKey))
            {
                return;
            }

            var cart = cartInfo[CurrentCartKey] as Cart;
            if (cart != null)
            {
                cart.InstanceId = Guid.NewGuid();
                if (HttpContext.Current == null && cartInfo.ContainsKey(CurrentContextKey))
                {
                    HttpContext.Current = cartInfo[CurrentContextKey] as HttpContext;
                }
                try
                {
                    cart.AcceptChanges();
                }
                catch (System.Exception ex)
                {
                    ErrorManager.GenerateError(ex.Message);
                }
            }
        }

        /// <summary>
        /// Strips a text to a given length without splitting the last word.
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="maxLength">Max length of the text</param>
        /// <returns>A shortened version of the given string</returns>
        /// <remarks>Will return empty string if input is null or empty</remarks>
        public static string StripPreviewText(this string source, int maxLength)
        {
            if (string.IsNullOrEmpty(source))
                return string.Empty;
            if (source.Length <= maxLength)
            {
                return source;
            }
            source = source.Substring(0, maxLength);
            // The maximum number of characters to cut from the end of the string.
            var maxCharCut = (source.Length > 15 ? 15 : source.Length - 1);
            var previousWord = source.LastIndexOfAny(new char[] { ' ', '.', ',', '!', '?' }, source.Length - 1, maxCharCut);
            if (previousWord >= 0)
            {
                source = source.Substring(0, previousWord);
            }
            return source + " ...";
        }

        public static string GetUrlValueFromStartPage(string propertyName)
        {
            var startPageData = DataFactory.Instance.GetPage(PageReference.StartPage);
            if (startPageData == null)
            {
                return PageReference.StartPage.GetFriendlyUrl();
            }

            string result = string.Empty;
            var property = startPageData.Property[propertyName];
            if (property != null && !property.IsNull)
            {
                if (property.PropertyValueType == typeof(PageReference))
                {
                    var propertyValue = property.Value as PageReference;
                    if (propertyValue != null)
                    {
                        result = propertyValue.GetFriendlyUrl();
                    }
                }
            }
            return string.IsNullOrEmpty(result) ? PageReference.StartPage.GetFriendlyUrl() : result;
        }

        /// <summary>
        /// Gets friendly url of the page.
        /// </summary>
        /// <param name="pageReference">The page reference.</param>
        /// <returns>The friendly url of page if UrlRewriteProvider.IsFurlEnabled</returns>
        public static string GetFriendlyUrl(this PageReference pageReference)
        {
            if (pageReference == null)
            {
                return string.Empty;
            }

            var page = DataFactory.Instance.GetPage(pageReference);

            if (UrlRewriteProvider.IsFurlEnabled)
            {
                var url = UriSupport.AddLanguageSelection(page.LinkURL, ContentLanguage.PreferredCulture.Name);

                // Get friendly url from permanent link without hostname.
                UrlBuilder urlBuilder = new UrlBuilder(url);
                Global.UrlRewriteProvider.ConvertToExternal(urlBuilder, page.PageLink, System.Text.Encoding.UTF8);

                // Then set hostname with port number back to url
                if (HttpContext.Current.Request != null)
                {
                    var requestUrl = HttpContext.Current.Request.Url;
                    urlBuilder.Host = requestUrl.Host;
                    urlBuilder.Port = requestUrl.Port;
                }

                return urlBuilder.ToString();
            }
            else
            {
                return page.LinkURL;
            }
        }

        /// <summary>
        /// Calculates the amount, to return the smallest unit of an amount in the selected currency.
        /// </summary>
        /// <remarks>http://tech.dibspayment.com/capturecgi</remarks>
        /// <param name="currency">Selected currency</param>
        /// <param name="amount">Amount in the selected currency</param>
        /// <returns>String represents the smallest unit of an amount in the selected currency.</returns>
        internal static string GetAmount(Currency currency, decimal amount)
        {
            int delta = currency.Equals(Currency.JPY) ? 1 : 100;
            return (amount * delta).ToString("#");
        }

        /// <summary>
        /// Convert the currency code of the site to
        /// the ISO4217 number for that currency for DIBS to understand.
        /// </summary>
        /// <param name="currency">The currency.</param>
        /// <returns></returns>
        public static string GetCurrencyCode(Currency currency)
        {
            if (currency.Equals(Currency.DKK))
                return DKK;
            else if (currency.Equals(Currency.AUD))
                return AUD;
            else if (currency.Equals(Currency.CAD))
                return CAD;
            else if (currency.Equals(Currency.CHF))
                return CHF;
            else if (currency.Equals(Currency.EUR))
                return EUR;
            else if (currency.Equals(Currency.GBP))
                return GBP;
            else if (currency.Equals(Currency.ISK))
                return ISK;
            else if (currency.Equals(Currency.JPY))
                return JPY;
            else if (currency.Equals(Currency.NOK))
                return NOK;
            else if (currency.Equals(Currency.NZD))
                return NZD;
            else if (currency.Equals(Currency.SEK))
                return SEK;
            else if (currency.Equals(Currency.TRY))
                return TRY;
            else if (currency.Equals(Currency.USD))
                return USD;
            return string.Empty;
        }
    }
}
