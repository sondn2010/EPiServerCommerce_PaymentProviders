using EPiServer.Commerce.Order;
using EPiServer.Core;
using EPiServer.ServiceLocation;
using Mediachase.Commerce;
using Mediachase.Commerce.Core;
using Mediachase.Commerce.Orders;
using Mediachase.Commerce.Orders.Dto;
using Mediachase.Commerce.Orders.Managers;
using Mediachase.Commerce.Plugins.Payment;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

namespace EPiServer.Business.Commerce.Payment.DIBS
{
    public class DIBSPaymentGateway : AbstractPaymentGateway, IPaymentPlugin
    {
        public const string UserParameter = "MerchantID";
        public const string PasswordParameter = "Password";
        public const string ProcessingUrl = "ProcessingUrl";

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

            if (_orderForm == null)
            {
                _orderForm = OrderGroup.Forms.FirstOrDefault(form => form.Payments.Contains(payment));
            }

            if (OrderGroup is IPurchaseOrder)
            {
                if (payment.TransactionType == TransactionType.Capture.ToString())
                {
                    //return true meaning the capture request is done,
                    //actual capturing must be done on DIBS.
                    string result = PostCaptureRequest(payment, OrderGroup);
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
                    string result = PostRefundRequest(payment, OrderGroup);
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
        private string PostRequest(IPayment payment, IOrderGroup orderGroup, string url)
        {
            WebClient webClient = new WebClient();
            NameValueCollection request = new NameValueCollection();
            var purchaseOrder = orderGroup as IPurchaseOrder;
            string orderId = purchaseOrder.OrderNumber;
            string transact = payment.TransactionID;
            string currencyCode = orderGroup.Currency;
            string amount = Utilities.GetAmount(new Currency(currencyCode), payment.Amount);
            request.Add("merchant", Merchant);
            request.Add("transact", transact);
            request.Add("amount", amount);

            request.Add("currency", currencyCode);
            request.Add("orderId", orderId);
            string md5 = Utilities.GetMD5KeyRefund(Merchant, orderId, transact, amount);
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
        /// <param name="orderGroup">The order group.</param>
        private string PostCaptureRequest(IPayment payment, IOrderGroup orderGroup)
        {
            return PostRequest(payment, orderGroup, "https://payment.architrade.com/cgi-bin/capture.cgi");
        }

        /// <summary>
        /// Posts the refund request to DIBS API.
        /// </summary>
        /// <param name="payment">The payment.</param>
        /// <param name="orderGroup">The order group.</param>
        private string PostRefundRequest(IPayment payment, IOrderGroup orderGroup)
        {
            return PostRequest(payment, orderGroup, "https://payment.architrade.com/cgi-adm/refund.cgi");
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
                    _merchant = Utilities.GetParameterByName(Payment, DIBSPaymentGateway.UserParameter).Value;
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
                    _password = Utilities.GetParameterByName(Payment, DIBSPaymentGateway.PasswordParameter).Value;
                }
                return _password;
            }
        }
    }
}