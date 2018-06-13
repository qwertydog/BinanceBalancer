using BinanceBalancer.Models.Implementations;
using BinanceBalancer.Presenters;
using BinanceBalancer.Views.Implementations;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BinanceBalancer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Run().GetAwaiter().GetResult();
        }

        private static async Task Run()
        {
            var apiKey = "xnQUgRZZ4gxg9zkawrNy2MMjbi65sEv4ioDGkvA86a7jrbznGkapVFXIwM0RW7tD";
            var apiSecret = "WB7TQ73CjonYHkv83p8DDa08ponvNcOIWtLREoBjdq743YrBOYPvYMSMI7dId9rZ";

            using (var model = await BinanceRepository.Create(apiKey, apiSecret))
            using (var view = new BalancerView())
            {
                var presenter = new BalancerPresenter(view, model);

                Application.Run(view);
            }
        }
    }
}
