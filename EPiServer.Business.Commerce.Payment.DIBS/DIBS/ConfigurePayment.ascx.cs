using System;
using Mediachase.Web.Console.Interfaces;
using Mediachase.Commerce.Orders.Dto;
using System.Data;

namespace EPiServer.Business.Commerce.Payment.DIBS
{
    public partial class ConfigurePayment : System.Web.UI.UserControl, IGatewayControl
    {
        // Fields
        private PaymentMethodDto _paymentMethodDto;
        private string _validationGroup;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurePayment"/> class.
        /// </summary>
        public ConfigurePayment()
        {
            this._validationGroup = string.Empty;
            this._paymentMethodDto = null;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            this.BindData();
        }

        /// <summary>
        /// Binds the data.
        /// </summary>
        public void BindData()
        {
            if ((this._paymentMethodDto != null) && (this._paymentMethodDto.PaymentMethodParameter != null))
            {
                PaymentMethodDto.PaymentMethodParameterRow parameterByName = null;
                parameterByName = this.GetParameterByName(DIBSPaymentGateway.UserParameter);
                if (parameterByName != null)
                {
                    this.User.Text = parameterByName.Value;
                }
                parameterByName = this.GetParameterByName(DIBSPaymentGateway.PasswordParameter);
                if (parameterByName != null)
                {
                    this.Password.Text = parameterByName.Value;
                }

                parameterByName = this.GetParameterByName(DIBSPaymentGateway.ProcessingUrl);
                if (parameterByName != null)
                {
                    this.ProcessingUrl.Text = parameterByName.Value;
                }
                parameterByName = this.GetParameterByName(DIBSPaymentGateway.MD5Key1);
                if (parameterByName != null)
                {
                    this.MD5key1.Text = parameterByName.Value;
                }
                parameterByName = this.GetParameterByName(DIBSPaymentGateway.MD5Key2);
                if (parameterByName != null)
                {
                    this.MD5key2.Text = parameterByName.Value;
                }
            }
            else
            {
                this.Visible = false;
            }
        }



        #region IGatewayControl Members

        /// <summary>
        /// Loads the object.
        /// </summary>
        /// <param name="dto">The dto.</param>
        public void LoadObject(object dto)
        {
            this._paymentMethodDto = dto as PaymentMethodDto;
        }

        /// <summary>
        /// Saves the changes.
        /// </summary>
        /// <param name="dto">The dto.</param>
        public void SaveChanges(object dto)
        {
            if (this.Visible)
            {
                this._paymentMethodDto = dto as PaymentMethodDto;
                if ((this._paymentMethodDto != null) && (this._paymentMethodDto.PaymentMethodParameter != null))
                {
                    Guid empty = Guid.Empty;
                    if (this._paymentMethodDto.PaymentMethod.Count > 0)
                    {
                        empty = this._paymentMethodDto.PaymentMethod[0].PaymentMethodId;
                    }
                    PaymentMethodDto.PaymentMethodParameterRow parameterByName = null;
                    parameterByName = this.GetParameterByName(DIBSPaymentGateway.UserParameter);
                    if (parameterByName != null)
                    {
                        parameterByName.Value = this.User.Text;
                    }
                    else
                    {
                        this.CreateParameter(this._paymentMethodDto, DIBSPaymentGateway.UserParameter, this.User.Text, empty);
                    }

                    parameterByName = this.GetParameterByName(DIBSPaymentGateway.PasswordParameter);
                    if (parameterByName != null)
                    {
                        parameterByName.Value = this.Password.Text;
                    }
                    else
                    {
                        this.CreateParameter(this._paymentMethodDto, DIBSPaymentGateway.PasswordParameter, this.Password.Text, empty);
                    }

                    parameterByName = this.GetParameterByName(DIBSPaymentGateway.ProcessingUrl);
                    if (parameterByName != null)
                    {
                        parameterByName.Value = this.ProcessingUrl.Text;
                    }
                    else
                    {
                        this.CreateParameter(this._paymentMethodDto, DIBSPaymentGateway.ProcessingUrl, this.ProcessingUrl.Text, empty);
                    }
                    parameterByName = this.GetParameterByName(DIBSPaymentGateway.MD5Key1);
                    if (parameterByName != null)
                    {
                        parameterByName.Value = this.MD5key1.Text;
                    }
                    else
                    {
                        this.CreateParameter(this._paymentMethodDto, DIBSPaymentGateway.MD5Key1, this.MD5key1.Text, empty);
                    }
                    parameterByName = this.GetParameterByName(DIBSPaymentGateway.MD5Key2);
                    if (parameterByName != null)
                    {
                        parameterByName.Value = this.MD5key2.Text;
                    }
                    else
                    {
                        this.CreateParameter(this._paymentMethodDto, DIBSPaymentGateway.MD5Key2, this.MD5key2.Text, empty);
                    }
                }
            }

        }

        private PaymentMethodDto.PaymentMethodParameterRow GetParameterByName(string name)
        {
            PaymentMethodDto.PaymentMethodParameterRow[] rowArray = (PaymentMethodDto.PaymentMethodParameterRow[])this._paymentMethodDto.PaymentMethodParameter.Select(string.Format("Parameter = '{0}'", name));
            if ((rowArray != null) && (rowArray.Length > 0))
            {
                return rowArray[0];
            }
            return null;
        }

        private void CreateParameter(PaymentMethodDto dto, string name, string value, Guid paymentMethodId)
        {
            PaymentMethodDto.PaymentMethodParameterRow row = dto.PaymentMethodParameter.NewPaymentMethodParameterRow();
            row.PaymentMethodId = paymentMethodId;
            row.Parameter = name;
            row.Value = value;
            if (row.RowState == DataRowState.Detached)
            {
                dto.PaymentMethodParameter.Rows.Add(row);
            }
        }

        /// <summary>
        /// Gets or sets the validation group.
        /// </summary>
        /// <value>The validation group.</value>
        public string ValidationGroup
        {
            get
            {
                return _validationGroup;
            }
            set
            {
                _validationGroup = value;
            }
        }

        #endregion
    }
}