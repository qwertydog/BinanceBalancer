using BinanceBalancer.Models;
using BinanceBalancer.Views;
using System;
using System.Windows.Forms;

namespace BinanceBalancer.Presenters
{
    public class BalancerPresenter
    {
        private readonly IBalancerView _view;
        private readonly IBinanceRepository _repository;

        public BalancerPresenter(IBalancerView view, IBinanceRepository repository)
        {
            _view = view;
            _view.IsOnlineCommand += view_IsOnlineCommand;

            _repository = repository;

            //UpdateCustomerListView();
        }

        private async void view_IsOnlineCommand(object sender, EventArgs e)
        {
            var serverIsOnline = await _repository.IsServerOnline();

            MessageBox.Show("Server is online? " + serverIsOnline.ToString());
        }
    }
}
