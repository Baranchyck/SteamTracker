using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace SteamTracker.UI.Views.Matchmaker
{
    /// <summary>
    /// Рядок учасника в лівій картці: чекбокс, аватар, нік. Змінюється під час роботи екрана
    /// (вибір, блокування при досягненні ліміту, «Бібліотека прихована» після пошуку), тому повідомляє про зміни.
    /// </summary>
    public sealed class ParticipantRow : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _canToggle;
        private bool _libraryHidden;

        public ParticipantRow(string name, string input, string avatarUrl, Brush avatarBrush, string initial,
            bool isCurrentUser, string? steamId)
        {
            Name = name;
            Input = input;
            AvatarUrl = avatarUrl;
            AvatarBrush = avatarBrush;
            Initial = initial;
            IsCurrentUser = isCurrentUser;
            SteamId = steamId;
            _canToggle = !isCurrentUser;
            _isSelected = isCurrentUser;
        }

        public string Name { get; }

        /// <summary>Те, що йде в сервіс: SteamID друга або текст, який користувач ввів сам.</summary>
        public string Input { get; }

        public string AvatarUrl { get; }
        public Brush AvatarBrush { get; }
        public string Initial { get; }
        public bool IsCurrentUser { get; }

        /// <summary>null — рядок додано вручну, SteamID стане відомий лише з відповіді сервісу.</summary>
        public string? SteamId { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        /// <summary>false — або це сам користувач (завжди в складі), або вже набрано максимум учасників.</summary>
        public bool CanToggle
        {
            get => _canToggle;
            set
            {
                if (Set(ref _canToggle, value))
                {
                    OnPropertyChanged(nameof(Opacity));
                }
            }
        }

        public bool LibraryHidden
        {
            get => _libraryHidden;
            set
            {
                if (Set(ref _libraryHidden, value))
                {
                    OnPropertyChanged(nameof(HasBadge));
                    OnPropertyChanged(nameof(BadgeText));
                }
            }
        }

        public bool HasBadge => IsCurrentUser || LibraryHidden;

        public string BadgeText => LibraryHidden ? "Бібліотека прихована" : "завжди";

        // Заблокованих через ліміт приглушуємо; сам користувач завжди виглядає звичайно
        public double Opacity => IsCurrentUser || CanToggle ? 1 : 0.45;

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(name);
            return true;
        }

        private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Рядок спільної гри: вже підготовлені значення для показу.</summary>
    /// <param name="FillWidth">Довжина заповненої частини смуги (зірочка), решта — у RestWidth.</param>
    public record CommonGameRow(
        int Rank,
        string Name,
        string IconUrl,
        string Caption,
        GridLength FillWidth,
        GridLength RestWidth,
        string Hours,
        string Tooltip);
}
