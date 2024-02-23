using ComicReader.Common.Constants;
using ComicReader.Database;
using ComicReader.Utils;
using Microsoft.UI.Xaml;
using System;
using System.ComponentModel;

namespace ComicReader.DesignData
{
    public enum PageArrangementType
    {
        Single, // 1 2 3 4 5
        DualCover, // 1 23 45
        DualCoverMirror, // 1 32 54
        DualNoCover, // 12 34 5
        DualNoCoverMirror, // 21 43 5
    }

    public class ReaderSettingViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool m_IsVertical = true;
        public bool IsVertical
        {
            get => m_IsVertical;
            set
            {
                m_IsVertical = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsVertical"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsLeftToRightVisible"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsRightToLeftVisible"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsContinuous"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("PageArrangementIndex"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DemoPageFlowDirection"));

                EventBus.Default.With(EventId.ReaderVerticalChanged).Emit(0);
                SaveReaderSettings();
            }
        }

        public void OnSetVertical(object sender, RoutedEventArgs e)
        {
            IsVertical = true;
        }

        public void OnSetHorizontal(object sender, RoutedEventArgs e)
        {
            IsVertical = false;
        }

        private bool m_IsLeftToRight = true;
        public bool IsLeftToRightVisible => !IsVertical && m_IsLeftToRight;
        public bool IsRightToLeftVisible => !IsVertical && !m_IsLeftToRight;

        public bool IsLeftToRight
        {
            get => m_IsLeftToRight;
            set
            {
                m_IsLeftToRight = value;
                PageFlowDirection = value ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsLeftToRightVisible"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsRightToLeftVisible"));
                SaveReaderSettings();
            }
        }

        public void OnSetLeftToRight(object sender, RoutedEventArgs e)
        {
            IsLeftToRight = true;
        }

        public void OnSetRightToLeft(object sender, RoutedEventArgs e)
        {
            IsLeftToRight = false;
        }

        private bool? m_IsVerticalContinuous = null;
        public bool IsVerticalContinuous
        {
            get => m_IsVerticalContinuous.HasValue && m_IsVerticalContinuous.Value;
            set
            {
                m_IsVerticalContinuous = value;

                if (IsVertical)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsContinuous"));
                }

                EventBus.Default.With(EventId.ReaderContinuousChanged).Emit(0);
                SaveReaderSettings();
            }
        }

        private bool? m_IsHorizontalContinuous = null;
        public bool IsHorizontalContinuous
        {
            get => m_IsHorizontalContinuous.HasValue && m_IsHorizontalContinuous.Value;
            set
            {
                m_IsHorizontalContinuous = value;

                if (!IsVertical)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsContinuous"));
                }

                EventBus.Default.With(EventId.ReaderContinuousChanged).Emit(0);
                SaveReaderSettings();
            }
        }

        public bool IsContinuous
        {
            get
            {
                bool? val = IsVertical ? m_IsVerticalContinuous : m_IsHorizontalContinuous;
                return val.HasValue && val.Value;
            }
            set
            {
                if (IsVertical)
                {
                    if (!m_IsVerticalContinuous.HasValue || m_IsVerticalContinuous.Value != value)
                    {
                        IsVerticalContinuous = value;
                    }
                }
                else
                {
                    if (!m_IsHorizontalContinuous.HasValue || m_IsHorizontalContinuous.Value != value)
                    {
                        IsHorizontalContinuous = value;
                    }
                }
            }
        }

        public void OnSetContinuous(object sender, RoutedEventArgs e)
        {
            IsContinuous = true;
        }

        public void OnSetDiscrete(object sender, RoutedEventArgs e)
        {
            IsContinuous = false;
        }

        private PageArrangementType m_VerticalPageArrangement = PageArrangementType.Single;
        public PageArrangementType VerticalPageArrangement
        {
            get => m_VerticalPageArrangement;
            set
            {
                m_VerticalPageArrangement = value;

                if (IsVertical)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(PageArrangementIndex)}"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsPageArrangementSingle)}"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsPageArrangementDualCover)}"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsPageArrangementDualNoCover)}"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsPageArrangementDualCoverMirror)}"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsPageArrangementDualNoCoverMirror)}"));
                }

                EventBus.Default.With(EventId.ReaderPageArrangementChanged).Emit(0);
                SaveReaderSettings();
            }
        }

        private PageArrangementType m_HorizontalPageArrangement = PageArrangementType.DualCover;
        public PageArrangementType HorizontalPageArrangement
        {
            get => m_HorizontalPageArrangement;
            set
            {
                m_HorizontalPageArrangement = value;

                if (!IsVertical)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(PageArrangementIndex)}"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsPageArrangementSingle)}"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsPageArrangementDualCover)}"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsPageArrangementDualNoCover)}"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsPageArrangementDualCoverMirror)}"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsPageArrangementDualNoCoverMirror)}"));
                }

                EventBus.Default.With(EventId.ReaderPageArrangementChanged).Emit(0);
                SaveReaderSettings();
            }
        }

        public PageArrangementType PageArrangement
        {
            get => IsVertical ? m_VerticalPageArrangement : m_HorizontalPageArrangement;
            set
            {
                if (IsVertical)
                {
                    if (m_VerticalPageArrangement != value)
                    {
                        VerticalPageArrangement = value;
                    }
                }
                else
                {
                    if (m_HorizontalPageArrangement != value)
                    {
                        HorizontalPageArrangement = value;
                    }
                }
            }
        }

        public int PageArrangementIndex
        {
            get
            {
                Func<PageArrangementType, int> to_index = (PageArrangementType page_arrangement) =>
                {
                    switch (page_arrangement)
                    {
                        case PageArrangementType.Single:
                            return 0;
                        case PageArrangementType.DualCover:
                            return 1;
                        case PageArrangementType.DualCoverMirror:
                            return 2;
                        case PageArrangementType.DualNoCover:
                            return 3;
                        case PageArrangementType.DualNoCoverMirror:
                            return 4;
                        default:
                            System.Diagnostics.Debug.Assert(false);
                            return 0;
                    }
                };

                return to_index(PageArrangement);
            }
            set
            {
                Func<int, PageArrangementType> from_index = (int index) =>
                {
                    switch (index)
                    {
                        case 0:
                            return PageArrangementType.Single;
                        case 1:
                            return PageArrangementType.DualCover;
                        case 2:
                            return PageArrangementType.DualCoverMirror;
                        case 3:
                            return PageArrangementType.DualNoCover;
                        case 4:
                            return PageArrangementType.DualNoCoverMirror;
                        default:
                            System.Diagnostics.Debug.Assert(false);
                            return PageArrangementType.Single;
                    }
                };

                PageArrangement = from_index(value);
            }
        }

        public bool IsPageArrangementSingle
        {
            get => PageArrangement == PageArrangementType.Single;
        }

        public bool IsPageArrangementDualCover
        {
            get => PageArrangement == PageArrangementType.DualCover;
        }

        public bool IsPageArrangementDualNoCover
        {
            get => PageArrangement == PageArrangementType.DualNoCover;
        }

        public bool IsPageArrangementDualCoverMirror
        {
            get => PageArrangement == PageArrangementType.DualCoverMirror;
        }

        public bool IsPageArrangementDualNoCoverMirror
        {
            get => PageArrangement == PageArrangementType.DualNoCoverMirror;
        }

        private FlowDirection m_PageFlowDirection = FlowDirection.LeftToRight;
        public FlowDirection PageFlowDirection
        {
            get => m_PageFlowDirection;
            set
            {
                m_PageFlowDirection = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("PageFlowDirection"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DemoPageFlowDirection"));
            }
        }

        public FlowDirection DemoPageFlowDirection => m_IsVertical ? FlowDirection.LeftToRight : m_PageFlowDirection;

        private void SaveReaderSettings()
        {
            Utils.C0.Run(async delegate
            {
                await XmlDatabaseManager.WaitLock();

                Database.XmlDatabase.Settings.VerticalReading = IsVertical;
                Database.XmlDatabase.Settings.LeftToRight = IsLeftToRight;
                Database.XmlDatabase.Settings.VerticalContinuous = IsVerticalContinuous;
                Database.XmlDatabase.Settings.HorizontalContinuous = IsHorizontalContinuous;
                Database.XmlDatabase.Settings.VerticalPageArrangement = VerticalPageArrangement;
                Database.XmlDatabase.Settings.HorizontalPageArrangement = HorizontalPageArrangement;

                XmlDatabaseManager.ReleaseLock();
                Utils.TaskQueue.DefaultQueue.Enqueue(XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Settings));
            });
        }
    }
}
