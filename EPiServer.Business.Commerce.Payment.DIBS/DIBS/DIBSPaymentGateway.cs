using EPiServer.Commerce.Order;
using EPiServer.Core;
using EPiServer.ServiceLocation;
using Mediachase.Commerce;
using Mediachase.Commerce.Core;
using Mediachase.Commerce.Orders;
using Mediachase.Commerce.Orders.Dto;
using Mediachase.Commerce.Orders.Managers;
using Mediachase.Commerce.Plugins.Payment;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace EPiServer.Business.Commerce.Payment.DIBS
{
    public class DIBSPaymentGateway : AbstractPaymentGateway, IPaymentPlugin
    {
        public const string UserParameter = "MerchantID";
        public const string PasswordParameter = "Password";
        public const string ProcessingUrl = "ProcessingUrl";
        public const string MD5Key1 = "MD5Key1";
        public const string MD5Key2 = "MD5Key2";

        public const string PaymentCompleted = "DIBS payment completed";

        private string _merchant;
        private string _password;
        private PaymentMethodDto _payment;
        private readonly Injected<IOrderRepository> _orderRepository;

        private IOrderForm _orderForm;

        public IOrderGroup OrderGroup { get; set; }

        public override bool ProcessPayment(Mediachase.Commerce.Orders.Payment payment, ref string message)
        {
            OrderGroup = payment.Parent.Parent;
            _orderForm = OrderGroup.Forms.FirstOrDefault(form => form.Payments.Contains(payment));
            return ProcessPayment(payment as IPayment, ref message);
        }

        public bool ProcessPayment(IPayment payment, ref string message)
        {
            if (HttpContext.Current == null)
            {
                return true;
            }

            if (OrderGroup is PurchaseOrder)
            {
                if (payment.TransactionType == TransactionType.Capture.ToString())
                {
                    //return true meaning the capture request is done,
                    //actual capturing must be done on DIBS.
                    string result = PostCaptureRequest(payment);
                    //result containing ACCEPTED means the request was successful
                    if (result.IndexOf("ACCEPTED") == -1)
                    {
                        message = "There was an error while capturing payment with DIBS";
                        return false;
                    }
                    return true;
                }

                if (payment.TransactionType == TransactionType.Credit.ToString())
                {
                    var transactionID = payment.TransactionID;
                    if (string.IsNullOrEmpty(transactionID) || transactionID.Equals("0"))
                    {
                        message = "TransactionID is not valid or the current payment method does not support this order type.";
                        return false;
                    }
                    //The transact must be captured before refunding
                    string result = PostRefundRequest(payment);
                    if (result.IndexOf("ACCEPTED") == -1)
                    {
                        message = "There was an error while refunding with DIBS";
                        return false;
                    }
                    return true;
                }
                //right now we do not support processing the order which is created by Commerce Manager
                message = "The current payment method does not support this order type.";
                return false;
            }

            var cart = OrderGroup as ICart;
            if (cart != null && cart.OrderStatus.ToString() == PaymentCompleted)
            {
                // return true because this shopping cart has been paid already on DIBS
                return true;
            }
            _orderRepository.Service.Save(OrderGroup);

            var pageRef = DataFactory.Instance.GetPage(PageReference.StartPage)["DIBSPaymentPage"] as PageReference;
            PageData page = DataFactory.Instance.GetPage(pageRef);
            HttpContext.Current.Response.Redirect(page.LinkURL);

            return false;
        }

        /// <summary>
        /// Posts the request to DIBS API.
        /// </summary>
        /// <param name="payment">The payment.</param>
        /// <param name="url">The URL.</param>
        /// <returns>A string contains result from DIBS API</returns>
        private string PostRequest(IPayment payment, string url)
        {
            WebClient webClient = new WebClient();
            NameValueCollection request = new NameValueCollection();
            var po = OrderGroup as PurchaseOrder;
            string orderid = po.TrackingNumber;
            string transact = payment.TransactionID;
            string currencyCode = OrderGroup.Currency;
            string amount = Utilities.GetAmount(new Currency(currencyCode), payment.Amount);
            request.Add("merchant", Merchant);
            request.Add("transact", transact);
            request.Add("amount", amount);

            request.Add("currency", currencyCode);
            request.Add("orderId", orderid);
            string md5 = GetMD5KeyRefund(Merchant, orderid, transact, amount);
            request.Add("md5key", md5);

            // in order to support split payment, let's set supportSplitPayment to true, and make sure you have enabled Split payment for your account
            // http://tech.dibspayment.com/flexwin_api_other_features_split_payment
            var supportSplitPayment = false;
            if (supportSplitPayment)
            {
                request.Add("splitpay", "true");
            }
            else
            {
                request.Add("force", "yes");
            }

            request.Add("textreply", "yes");
            webClient.Credentials = new NetworkCredential(Merchant, Password);
            byte[] responseArray = webClient.UploadValues(url, "POST", request);
            return Encoding.ASCII.GetString(responseArray);
        }

        /// <summary>
        /// Posts the capture request to DIBS API.
        /// </summary>
        /// <param name="payment">The payment.</param>
        /// <returns>Return string from DIBS API</returns>
        private string PostCaptureRequest(IPayment payment)
        {
            return PostRequest(payment, "https://payment.architrade.com/cgi-bin/capture.cgi");
        }

        /// <summary>
        /// Posts the refund request to DIBS API.
        /// </summary>
        /// <param name="payment">The payment.</param>
        private string PostRefundRequest(IPayment payment)
        {
            return PostRequest(payment, "https://payment.architrade.com/cgi-adm/refund.cgi");
        }

        /// <summary>
        /// Gets the payment.
        /// </summary>
        /// <value>The payment.</value>
        public PaymentMethodDto Payment
        {
            get
            {
                if (_payment == null)
                {
                    _payment = PaymentManager.GetPaymentMethodBySystemName("DIBS", SiteContext.Current.LanguageName);
                }
                return _payment;
            }
        }

        /// <summary>
        /// Gets the merchant.
        /// </summary>
        /// <value>The merchant.</value>
        public string Merchant
        {
            get
            {
                if (string.IsNullOrEmpty(_merchant))
                {
                    _merchant = GetParameterByName(Payment, DIBSPaymentGateway.UserParameter).Value;
                }
                return _merchant;
            }
        }

        /// <summary>
        /// Gets the password.
        /// </summary>
        /// <value>The password.</value>
        public string Password
        {
            get
            {
                if (string.IsNullOrEmpty(_password))
                {
                    _password = GetParameterByName(Payment, DIBSPaymentGateway.PasswordParameter).Value;
                }
                return _password;
            }
        }


        /// <summary>
        /// Gets the M d5 key refund.
        /// </summary>
        /// <param name="merchant">The merchant.</param>
        /// <param name="orderId">The order id.</param>
        /// <param name="transact">The transact.</param>
        /// <param name="amount">The amount.</param>
        /// <returns></returns>
        public static string GetMD5KeyRefund(string merchant, string orderId, string transact, string amount)
        {
            string hashString = string.Format("merchant={0}&orderid={1}&transact={2}&amount={3}", 
                                                merchant,
                                                orderId, 
                                                transact, 
                                                amount);
            return GetMD5Key(hashString);
        }

        /// <summary>
        /// Gets the MD5 key used to send to DIBS in authorization step.
        /// </summary>
        /// <param name="merchant">The merchant.</param>
        /// <param name="orderId">The order id.</param>
        /// <param name="currency">The currency.</param>
        /// <param name="amount">The amount.</param>
        /// <returns></returns>
        public static string GetMD5Key(string merchant, string orderId, Currency currency, string amount)
        {
            string hashString = string.Format("merchant={0}&orderid={1}&currency={2}&amount={3}", 
                                                merchant,
                                                orderId,
                                                currency.CurrencyCode, 
                                                amount);
            return GetMD5Key(hashString);
        }

        /// <summary>
        /// Gets the key used to verify response from DIBS when payment is approved.
        /// </summary>
        /// <param name="transact">The transact.</param>
        /// <param name="amount">The amount.</param>
        /// <param name="currency">The currency.</param>
        /// <returns></returns>
        public static string GetMD5Key(string transact, string amount, Currency currency)
        {
            string hashString = string.Format("transact={0}&amount={1}&currency={2}", 
                                                transact, 
                                                amount,
                                                Utilities.GetCurrencyCode(currency));
            return GetMD5Key(hashString);
        }

        private static string GetMD5Key(string hashString)
        {
            PaymentMethodDto dibs = PaymentManager.GetPaymentMethodBySystemName("DIBS", SiteContext.Current.LanguageName);
            string key1 = GetParameterByName(dibs, MD5Key1).Value;
            string key2 = GetParameterByName(dibs, MD5Key2).Value;

            MD5CryptoServiceProvider x = new MD5CryptoServiceProvider();
            byte[] bs = System.Text.Encoding.UTF8.GetBytes(key1 + hashString);
            bs = x.ComputeHash(bs);
            StringBuilder s = new StringBuilder();
            foreach (byte b in bs)
            {
                s.Append(b.ToString("x2").ToLower());
            }
            string firstHash = s.ToString();

            string secondHashString = key2 + firstHash;
            byte[] bs2 = System.Text.Encoding.UTF8.GetBytes(secondHashString);
            bs2 = x.ComputeHash(bs2);
            StringBuilder s2 = new StringBuilder();
            foreach (byte b in bs2)
            {
                s2.Append(b.ToString("x2").ToLower());
            }
            string secondHash = s2.ToString();
            return secondHash;
        }

        /// <summary>
        /// Gets the parameter by name.
        /// </summary>
        /// <param name="paymentMethodDto">The payment method dto.</param>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        internal static PaymentMethodDto.PaymentMethodParameterRow GetParameterByName(PaymentMethodDto paymentMethodDto, string name)
        {
            PaymentMethodDto.PaymentMethodParameterRow[] rowArray = (PaymentMethodDto.PaymentMethodParameterRow[])paymentMethodDto.PaymentMethodParameter.Select(string.Format("Parameter = '{0}'", name));
            if ((rowArray != null) && (rowArray.Length > 0))
            {
                return rowArray[0];
            }
            throw new ArgumentNullException("Parameter named " + name + " for DIBS payment cannot be null");
        }
    }
}
