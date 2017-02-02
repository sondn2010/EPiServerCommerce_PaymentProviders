using EPiServer.Business.Commerce.Payment.DIBS.PageTypes;
using EPiServer.Commerce.Order;
using EPiServer.Core;
using EPiServer.Framework.DataAnnotations;
using EPiServer.Security;
using EPiServer.ServiceLocation;
using Mediachase.Commerce;
using Mediachase.Commerce.Core;
using Mediachase.Commerce.Core.Features;
using Mediachase.Commerce.Customers;
using Mediachase.Commerce.Extensions;
using Mediachase.Commerce.Orders;
using Mediachase.Commerce.Orders.Dto;
using Mediachase.Commerce.Orders.Managers;
using Mediachase.Commerce.Security;
using Mediachase.Data.Provider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EPiServer.Business.Commerce.Payment.DIBS
{
    // TODO change to the appropriate path of the template.
    [TemplateDescriptor(Path = "~/Features/Payment/DIBSPayment.aspx")]
    public partial class DIBSPayment : TemplatePage<DIBSPaymentPage>
    {
        private ICart _currentCart = null;
        private readonly Injected<IOrderRepository> _orderRepository;
        private readonly Injected<IFeatureSwitch> _featureSwitch;
        private readonly Injected<IInventoryProcessor> _inventoryProcessor;

        public const string SessionLatestOrderIdKey = "LatestOrderId";
        public const string SessionLatestOrderNumberKey = "LatestOrderNumber";
        public const string SessionLatestOrderTotalKey = "LatestOrderTotal";
        public const string DIBSSystemName = "DIBS";

        private IPayment payment;
        private string _acceptUrl;
        private string _cancelUrl;
        private string _callbackUrl;
        private string _orderNumber;

        private ICart CurrentCart
        {
            get
            {
                if (_currentCart == null)
                {
                    _currentCart = _orderRepository.Service.LoadCart<ICart>(CustomerContext.Current.CurrentContactId, Cart.DefaultName);
                }

                return _currentCart;
            }
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load"/> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs"/> object that contains the event data.</param>
        protected override void OnLoad(EventArgs e)
        {
            if (!CurrentCart.GetFirstForm().Payments.Any())
            {
                return;
            }
            var payments = CurrentCart.GetFirstForm().Payments.ToArray();
            //get DIBS payment by PaymentMethodName, instead of get the first payment in the list, which causes problem when checking out with gift card
            PaymentMethodDto dibsPaymentMethod = PaymentManager.GetPaymentMethodBySystemName(DIBSSystemName, SiteContext.Current.LanguageName);

            payment = payments.Where(c => c.PaymentMethodId.Equals(dibsPaymentMethod.PaymentMethod.Rows[0]["PaymentMethodId"])).FirstOrDefault();
            if (payment == null)
            {
                return;
            }

            string processingUrl = Utilities.GetParameterByName(dibsPaymentMethod, DIBSPaymentGateway.ProcessingUrl).Value;
            string MD5K1 = Utilities.GetParameterByName(dibsPaymentMethod, Utilities.MD5Key1).Value;
            string MD5K2 = Utilities.GetParameterByName(dibsPaymentMethod, Utilities.MD5Key2).Value;

            // Get DIBSPaymentLanding url
            if (!Request.IsAuthenticated)
            {
                _acceptUrl = "~/Templates/Sample/Pages/CheckoutConfirmationStep.aspx";
            }
            else
            {
                var referencePage = DataFactory.Instance.GetPage(PageReference.StartPage)["DIBSPaymentLandingPage"] as PageReference;
                var landingPage = DataFactory.Instance.GetPage(referencePage);
                _acceptUrl = landingPage != null ? landingPage.LinkURL : string.Empty;
            }

            _cancelUrl = Request.UrlReferrer != null ? Request.UrlReferrer.ToString() : DataFactory.Instance.GetPage(PageReference.StartPage).LinkURL;
            //In case the user cancels the payment, he'll be redirected back to checkout page.
            //We need to set cancel url to post form.
            cancelurl.Value = _cancelUrl;

            paymentForm.Action = processingUrl;

            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetExpires(DateTime.Now.AddSeconds(-1));
            Response.Cache.SetNoStore();
            Response.AppendHeader("Pragma", "no-cache");

            // Process successful transaction                        
            if (CurrentCart != null && Request.Form["authkey"] != null
                && Request.Form["amount"] != null && Request.Form["currency"] != null &&
                MD5K1 != null && MD5K2 != null
                && Request.Form["orderid"] != null && Request.Form["transact"] != null)
            {
                _orderNumber = Request["orderid"];
                string myHash = Utilities.GetMD5Key(Request.Form["transact"], Request.Form["amount"],
                                                new Currency(Request.Form["currency"]));
                if (myHash.Equals(Request.Form["authkey"]))
                {
                    ProcessSuccessfulTransaction();
                }
            }
            //Process unsuccessful transaction
            else if (CurrentCart != null && Request.Form["merchant"] != null && Request.Form["orderid"] != null
                && Request.Form["amount"] != null && Request.Form["currency"] != null &&
                Request.Form["md5key"] != null)
            {
                string myHash = Utilities.GetMD5Key(Request.Form["merchant"], Request.Form["orderid"],
                                                new Currency(Request.Form["currency"]), Request.Form["amount"]);
                if (myHash.Equals(Request.Form["md5key"]))
                {
                    ProcessUnsuccessfulTransaction(string.Empty);
                }
            }

            if (payment == null)
            {
                Response.Redirect(_cancelUrl);
            }
            else if (payment != null)
            {
                // process request from Checkout
                _orderNumber = GenerateOrderNumber(CurrentCart);
            }

            //Page.ClientScript.RegisterClientScriptBlock(typeof(DIBSPayment), "submitScript", "window.onload = function (evt) { document.forms[0].submit(); }", true);
            base.OnLoad(e);
        }

        /// <summary>
        /// Processes the successful transaction.
        /// </summary>
        private void ProcessSuccessfulTransaction()
        {
            IPurchaseOrder purchaseOrder;
            ICart cart = CurrentCart;
            string email = payment.BillingAddress.Email;

            if (cart != null)
            {
                // Make sure to execute within transaction
                using (TransactionScope scope = new TransactionScope())
                {
                    cart.OrderStatus = OrderStatus.InProgress;

                    // Change status of payments to processed.
                    // It must be done before execute workflow to ensure payments which should mark as processed.
                    // To avoid get errors when executed workflow.
                    PaymentStatusManager.ProcessPayment(payment);

                    // Execute CheckOutWorkflow with parameter to ignore running process payment activity again.
                    var isIgnoreProcessPayment = new Dictionary<string, object>();
                    isIgnoreProcessPayment.Add("PreventProcessPayment", true);
                    if (_featureSwitch.Service.IsSerializedCartsEnabled())
                    {
                        cart.AdjustInventoryOrRemoveLineItems((item, issue) => AddValidationIssues(item, issue), _inventoryProcessor.Service);
                    }
                    else
                    {
                        OrderGroupWorkflowManager.RunWorkflow((OrderGroup)cart, OrderGroupWorkflowManager.CartCheckOutWorkflowName, true, isIgnoreProcessPayment);
                    }

                    //Save the transact from DIBS to payment.
                    payment.TransactionID = Request["transact"];

                    // Save changes
                    //cart.OrderNumberMethod = new Cart.CreateOrderNumber((c) => _orderNumber);
                    //this might cause problem when checkout using multiple shipping address because ECF workflow does not handle it. Modify the workflow instead of modify in this payment

                    var poLink = _orderRepository.Service.SaveAsPurchaseOrder(cart);
                    purchaseOrder = _orderRepository.Service.Load(poLink) as IPurchaseOrder;
                    ((PurchaseOrder)purchaseOrder).Status = DIBSPaymentGateway.PaymentCompleted;

                    purchaseOrder.OrderNumber = _orderNumber;

                    if (CustomerContext.Current.CurrentContact != null)
                    {
                        CustomerContext.Current.CurrentContact.LastOrder = purchaseOrder.Created;
                        CustomerContext.Current.CurrentContact.SaveChanges();
                    }

                    AddNoteToPurchaseOrder("New order placed by {0} in {1}", purchaseOrder, PrincipalInfo.CurrentPrincipal.Identity.Name, "Public site");

                    // Update display name of product by current language
                    purchaseOrder.UpdateDisplayNameWithCurrentLanguage();
                    _orderRepository.Service.Save(purchaseOrder);

                    // Remove old cart
                    _orderRepository.Service.Delete(cart.OrderLink);

                    // Commit changes
                    scope.Complete();

                    if (HttpContext.Current.Session != null)
                    {
                        HttpContext.Current.Session.Remove("LastCouponCode");
                    }
                }
                _acceptUrl = UriSupport.AddQueryString(_acceptUrl, "success", "true");
                _acceptUrl = UriSupport.AddQueryString(_acceptUrl, "ordernumber", purchaseOrder.OrderNumber);
                _acceptUrl = UriSupport.AddQueryString(_acceptUrl, "email", email);
                Response.Redirect(_acceptUrl, true);
            }
            else
            {
                Response.Redirect(_cancelUrl, true);
            }
        }

        /// <summary>
        /// Adds the note to purchase order.
        /// </summary>
        /// <param name="note">Name of the note.</param>
        /// <param name="purchaseOrder">The purchase order.</param>
        /// <param name="parmeters">The parameters.</param>
        protected void AddNoteToPurchaseOrder(string note, IPurchaseOrder purchaseOrder, params string[] parmeters)
        {
            var noteDetails = string.Format(note, parmeters);

            var orderNote = purchaseOrder.CreateOrderNote();
            orderNote.Type = OrderNoteTypes.System.ToString();
            orderNote.CustomerId = PrincipalInfo.CurrentPrincipal.GetContactId();
            orderNote.Title = noteDetails.Substring(0, Math.Min(noteDetails.Length, 24)) + "...";
            orderNote.Detail = noteDetails;
            orderNote.Created = DateTime.UtcNow;
            purchaseOrder.Notes.Add(orderNote);
        }

        private void ProcessUnsuccessfulTransaction(string error)
        {
            Response.Redirect(_cancelUrl, true);
        }

        /// <summary>
        /// Gets the MD5 authentication key to send to DIBS.
        /// </summary>
        public string MD5Key
        {
            get
            {
                return Utilities.GetMD5Key(MerchantID, OrderID, Currency, Amount);
            }
        }

        /// <summary>
        /// Generates the order number.
        /// </summary>
        /// <param name="cart">The cart.</param>
        /// <returns></returns>
        private string GenerateOrderNumber(ICart cart)
        {
            string str = new Random().Next(100, 999).ToString();
            return string.Format("PO{0}{1}", cart.OrderLink.OrderGroupId, str);
        }

        /// <summary>
        /// Gets the merchant ID.
        /// </summary>
        /// <value>The merchant ID.</value>
        public string MerchantID
        {
            get
            {
                PaymentMethodDto dibs = PaymentManager.GetPaymentMethodBySystemName("DIBS", SiteContext.Current.LanguageName);
                return Utilities.GetParameterByName(dibs, DIBSPaymentGateway.UserParameter).Value;
            }
        }

        /// <summary>
        /// Gets the amount.
        /// </summary>
        /// <value>The amount.</value>
        public string Amount
        {
            get
            {
                return (payment != null) ? Utilities.GetAmount(Currency, payment.Amount) : string.Empty;
            }
        }

        /// <summary>
        /// Gets the currency code.
        /// </summary>
        /// <value>The currency code.</value>
        public Currency Currency
        {
            get
            {
                if (payment == null)
                {
                    return string.Empty;
                }
                return string.IsNullOrEmpty(CurrentCart.Currency) ?
                    SiteContext.Current.Currency : CurrentCart.Currency;
            }
        }

        /// <summary>
        /// Gets the order ID.
        /// </summary>
        /// <value>The order ID.</value>
        public string OrderID
        {
            get
            {
                return _orderNumber;
            }
        }

        /// <summary>
        /// Gets the callback URL, which is used by DIBS to call back when
        /// transaction is approved.
        /// </summary>
        /// <value>The callback URL.</value>
        public string CallbackUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_callbackUrl))
                {
                    _callbackUrl = Request.Url.ToString();
                }
                return _callbackUrl;
            }
        }

        /// <summary>
        /// Convert the site language to the language which DIBS can support.
        /// </summary>
        /// <value>The language.</value>
        public string Language
        {
            get
            {
                if (SiteContext.Current.LanguageName.StartsWith("da", StringComparison.OrdinalIgnoreCase))
                    return "da";
                else if (SiteContext.Current.LanguageName.StartsWith("sv", StringComparison.OrdinalIgnoreCase))
                    return "sv";
                else if (SiteContext.Current.LanguageName.StartsWith("no", StringComparison.OrdinalIgnoreCase))
                    return "no";
                else if (SiteContext.Current.LanguageName.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                    return "en";
                else if (SiteContext.Current.LanguageName.StartsWith("nl", StringComparison.OrdinalIgnoreCase))
                    return "nl";
                else if (SiteContext.Current.LanguageName.StartsWith("de", StringComparison.OrdinalIgnoreCase))
                    return "de";
                else if (SiteContext.Current.LanguageName.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
                    return "fr";
                else if (SiteContext.Current.LanguageName.StartsWith("fi", StringComparison.OrdinalIgnoreCase))
                    return "fi";
                else if (SiteContext.Current.LanguageName.StartsWith("es", StringComparison.OrdinalIgnoreCase))
                    return "es";
                else if (SiteContext.Current.LanguageName.StartsWith("it", StringComparison.OrdinalIgnoreCase))
                    return "it";
                else if (SiteContext.Current.LanguageName.StartsWith("fo", StringComparison.OrdinalIgnoreCase))
                    return "fo";
                else if (SiteContext.Current.LanguageName.StartsWith("pl", StringComparison.OrdinalIgnoreCase))
                    return "pl";
                return "en";
            }
        }

        private static IDictionary<ILineItem, List<ValidationIssue>> AddValidationIssues(ILineItem lineItem, ValidationIssue issue)
        {
            var issues = new Dictionary<ILineItem, List<ValidationIssue>>();
            if (!issues.ContainsKey(lineItem))
            {
                issues.Add(lineItem, new List<ValidationIssue>());
            }

            if (!issues[lineItem].Contains(issue))
            {
                issues[lineItem].Add(issue);
            }
            return issues;
        }
    }
}