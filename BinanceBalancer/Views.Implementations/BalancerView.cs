using BinanceBalancer.ViewModels;
using System;
using System.Windows.Forms;

namespace BinanceBalancer.Views.Implementations
{
    public partial class BalancerView : Form, IBalancerView
    {
        public BalancerViewModel BalancerViewModel
        {
            set
            {
                //connectedLabel.DataBindings.Add(new Binding("Text", value, nameof(BalancerViewModel.), true, DataSourceUpdateMode.OnPropertyChanged, string.Empty));
                //txtLastName.DataBindings.Add(new Binding("Text", value, "LastName", true, DataSourceUpdateMode.OnPropertyChanged, string.Empty));
            }
        }

        public bool IsConnected
        {
            get { return connectedLabel.Text == "Connected"; }
            set { connectedLabel.Text = (value ? "Connected" : "Not Connected"); }
        }

        public event EventHandler IsOnlineCommand;

        public BalancerView()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            IsOnlineCommand?.Invoke(sender, e);
        }
    }
}
