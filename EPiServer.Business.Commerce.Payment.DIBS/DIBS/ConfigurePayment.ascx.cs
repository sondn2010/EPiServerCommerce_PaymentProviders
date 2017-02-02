using System;
using Mediachase.Web.Console.Interfaces;
using Mediachase.Commerce.Orders.Dto;
using System.Data;

namespace EPiServer.Business.Commerce.Payment.DIBS
{
    public partial class ConfigurePayment : System.Web.UI.UserControl, IGatewayControl
    {
        private PaymentMethodDto _paymentMethodDto;
        private string _validationGroup;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurePayment"/> class.
        /// </summary>
        public ConfigurePayment()
        {
            _validationGroup = string.Empty;
            _paymentMethodDto = null;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            BindData();
        }

        /// <summary>
        /// Binds the data.
        /// </summary>
        public void BindData()
        {
            if ((_paymentMethodDto != null) && (_paymentMethodDto.PaymentMethodParameter != null))
            {
                PaymentMethodDto.PaymentMethodParameterRow parameterByName = null;
                parameterByName = GetParameterByName(DIBSPaymentGateway.UserParameter);
                if (parameterByName != null)
                {
                    User.Text = parameterByName.Value;
                }
                parameterByName = GetParameterByName(DIBSPaymentGateway.PasswordParameter);
                if (parameterByName != null)
                {
                    Password.Text = parameterByName.Value;
                }

                parameterByName = GetParameterByName(DIBSPaymentGateway.ProcessingUrl);
                if (parameterByName != null)
                {
                    ProcessingUrl.Text = parameterByName.Value;
                }
                parameterByName = GetParameterByName(Utilities.MD5Key1);
                if (parameterByName != null)
                {
                    MD5key1.Text = parameterByName.Value;
                }
                parameterByName = GetParameterByName(Utilities.MD5Key2);
                if (parameterByName != null)
                {
                    MD5key2.Text = parameterByName.Value;
                }
            }
            else
            {
                Visible = false;
            }
        }

        /// <summary>
        /// Loads the object.
        /// </summary>
        /// <param name="dto">The dto.</param>
        public void LoadObject(object dto)
        {
            _paymentMethodDto = dto as PaymentMethodDto;
        }

        /// <summary>
        /// Saves the changes.
        /// </summary>
        /// <param name="dto">The dto.</param>
        public void SaveChanges(object dto)
        {
            if (Visible)
            {
                _paymentMethodDto = dto as PaymentMethodDto;
                if ((_paymentMethodDto != null) && (_paymentMethodDto.PaymentMethodParameter != null))
                {
                    Guid empty = Guid.Empty;
                    if (_paymentMethodDto.PaymentMethod.Count > 0)
                    {
                        empty = _paymentMethodDto.PaymentMethod[0].PaymentMethodId;
                    }
                    PaymentMethodDto.PaymentMethodParameterRow parameterByName = null;
                    parameterByName = GetParameterByName(DIBSPaymentGateway.UserParameter);
                    if (parameterByName != null)
                    {
                        parameterByName.Value = User.Text;
                    }
                    else
                    {
                        CreateParameter(_paymentMethodDto, DIBSPaymentGateway.UserParameter, User.Text, empty);
                    }

                    parameterByName = GetParameterByName(DIBSPaymentGateway.PasswordParameter);
                    if (parameterByName != null)
                    {
                        parameterByName.Value = Password.Text;
                    }
                    else
                    {
                        CreateParameter(_paymentMethodDto, DIBSPaymentGateway.PasswordParameter, Password.Text, empty);
                    }

                    parameterByName = GetParameterByName(DIBSPaymentGateway.ProcessingUrl);
                    if (parameterByName != null)
                    {
                        parameterByName.Value = ProcessingUrl.Text;
                    }
                    else
                    {
                        CreateParameter(_paymentMethodDto, DIBSPaymentGateway.ProcessingUrl, ProcessingUrl.Text, empty);
                    }
                    parameterByName = GetParameterByName(Utilities.MD5Key1);
                    if (parameterByName != null)
                    {
                        parameterByName.Value = MD5key1.Text;
                    }
                    else
                    {
                        CreateParameter(_paymentMethodDto, Utilities.MD5Key1, MD5key1.Text, empty);
                    }
                    parameterByName = GetParameterByName(Utilities.MD5Key2);
                    if (parameterByName != null)
                    {
                        parameterByName.Value = MD5key2.Text;
                    }
                    else
                    {
                        CreateParameter(_paymentMethodDto, Utilities.MD5Key2, MD5key2.Text, empty);
                    }
                }
            }

        }

        private PaymentMethodDto.PaymentMethodParameterRow GetParameterByName(string name)
        {
            PaymentMethodDto.PaymentMethodParameterRow[] rowArray = (PaymentMethodDto.PaymentMethodParameterRow[])_paymentMethodDto.PaymentMethodParameter.Select(string.Format("Parameter = '{0}'", name));
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
    }
}