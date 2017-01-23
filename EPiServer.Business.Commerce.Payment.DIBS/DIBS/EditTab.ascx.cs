using System;
using Mediachase.Web.Console.Interfaces;
using Mediachase.Commerce.Orders.Dto;
using System.Data;
using Mediachase.Web.Console.BaseClasses;

namespace EPiServer.Business.Commerce.Payment.DIBS
{
    /// <summary>
    /// Code behind for EditTab.ascx
    /// </summary>
    public partial class EditTab : CoreBaseUserControl, IAdminContextControl, IAdminTabControl
    {
        #region IAdminContextControl Members

        /// <summary>
        /// Loads the context.
        /// </summary>
        /// <param name="context">The context.</param>
        public void LoadContext(System.Collections.IDictionary context)
        {

        }

        #endregion

        #region IAdminTabControl Members

        /// <summary>
        /// Saves the changes.
        /// </summary>
        /// <param name="context">The context.</param>
        public void SaveChanges(System.Collections.IDictionary context)
        {
            
        }

        #endregion
    }
}