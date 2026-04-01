using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PriceDropCatcher.Models
{
    public class PriceResult : INotifyPropertyChanged
    {
        private bool _isBestDeal;

        public string Id { get; set; }
        public string Title { get; set; }
        public string Platform { get; set; }
        public string PriceDisplay { get; set; }
        public decimal? PriceUsd { get; set; }
        public double? Rating { get; set; }
        public string Offer { get; set; }
        public string Link { get; set; }
        public string Thumbnail { get; set; }

        public bool IsBestDeal
        {
            get => _isBestDeal;
            set { if (_isBestDeal == value) return; _isBestDeal = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
