using EPiServer.Core;
using EPiServer.DataAnnotations;

namespace EPiServer.Business.Commerce.Payment.DIBS.PageTypes
{
    [ContentType(GUID = "afa29655-74ce-45b8-abe7-696462a6efde",
        DisplayName = "DIBS Payment Page",
        Description = "",
        GroupName = "Payment pages",
        Order = 100)]
    public class DIBSPaymentPage : PageData
    {
    }
}
