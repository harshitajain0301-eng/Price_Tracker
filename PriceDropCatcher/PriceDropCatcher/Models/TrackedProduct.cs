using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PriceDropCatcher.Models
{
    public class TrackedProduct : INotifyPropertyChanged
    {
        private long _id;
        private string _name;
        private string _url;
        private decimal? _threshold;
        private decimal? _lastMinPrice;
        private DateTime? _lastCheckedUtc;
        private DateTime _createdAtUtc;

        public long Id
        {
            get => _id;
            set { if (_id == value) return; _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { if (_name == value) return; _name = value; OnPropertyChanged(); }
        }

        public string Url
        {
            get => _url;
            set { if (_url == value) return; _url = value; OnPropertyChanged(); }
        }

        public decimal? Threshold
        {
            get => _threshold;
            set { if (_threshold == value) return; _threshold = value; OnPropertyChanged(); }
        }

        public decimal? LastMinPrice
        {
            get => _lastMinPrice;
            set { if (_lastMinPrice == value) return; _lastMinPrice = value; OnPropertyChanged(); }
        }

        public DateTime? LastCheckedUtc
        {
            get => _lastCheckedUtc;
            set { if (_lastCheckedUtc == value) return; _lastCheckedUtc = value; OnPropertyChanged(); }
        }

        public DateTime CreatedAtUtc
        {
            get => _createdAtUtc;
            set { if (_createdAtUtc == value) return; _createdAtUtc = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
