using System;

namespace BinanceBalancer.Views
{
    public interface IBalancerView
    {
        //IList<string> CustomerList { get; set; }

        //int SelectedCustomer { get; set; }

        //string CustomerName { get; set; }

        //string Address { get; set; }

        //string Phone { get; set; }

        bool IsConnected { get; set; }

        event EventHandler IsOnlineCommand;
    }
}