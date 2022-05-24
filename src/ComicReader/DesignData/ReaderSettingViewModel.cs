﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace ComicReader.DesignData
{
    public enum PageArrangementEnum
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

                if (IsVerticalContinuous != IsHorizontalContinuous)
                {
                    OnContinuousChanged?.Invoke();
                }

                OnVerticalChanged?.Invoke();
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
                OnFlowDirectionChanged?.Invoke();
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

                OnContinuousChanged?.Invoke();
                OnVerticalContinuousChanged?.Invoke();
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

                OnContinuousChanged?.Invoke();
                OnHorizontalContinuousChanged?.Invoke();
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

        private PageArrangementEnum m_VerticalPageArrangement = PageArrangementEnum.Single;
        public PageArrangementEnum VerticalPageArrangement
        {
            get => m_VerticalPageArrangement;
            set
            {
                m_VerticalPageArrangement = value;

                if (IsVertical)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("PageArrangementIndex"));
                }

                OnVerticalPageArrangementChanged?.Invoke();
            }
        }

        private PageArrangementEnum m_HorizontalPageArrangement = PageArrangementEnum.DualCover;
        public PageArrangementEnum HorizontalPageArrangement
        {
            get => m_HorizontalPageArrangement;
            set
            {
                m_HorizontalPageArrangement = value;

                if (!IsVertical)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("PageArrangementIndex"));
                }

                OnHorizontalPageArrangementChanged?.Invoke();
            }
        }

        public PageArrangementEnum PageArrangement
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
                Func<PageArrangementEnum, int> to_index = (PageArrangementEnum page_arrangement) =>
                {
                    switch (page_arrangement)
                    {
                        case PageArrangementEnum.Single:
                            return 0;
                        case PageArrangementEnum.DualCover:
                            return 1;
                        case PageArrangementEnum.DualCoverMirror:
                            return 2;
                        case PageArrangementEnum.DualNoCover:
                            return 3;
                        case PageArrangementEnum.DualNoCoverMirror:
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
                Func<int, PageArrangementEnum> from_index = (int index) =>
                {
                    switch (index)
                    {
                        case 0:
                            return PageArrangementEnum.Single;
                        case 1:
                            return PageArrangementEnum.DualCover;
                        case 2:
                            return PageArrangementEnum.DualCoverMirror;
                        case 3:
                            return PageArrangementEnum.DualNoCover;
                        case 4:
                            return PageArrangementEnum.DualNoCoverMirror;
                        default:
                            System.Diagnostics.Debug.Assert(false);
                            return PageArrangementEnum.Single;
                    }
                };

                PageArrangement = from_index(value);
            }
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

        public Action OnVerticalChanged;
        public Action OnFlowDirectionChanged;
        public Action OnContinuousChanged;
        public Action OnVerticalContinuousChanged;
        public Action OnHorizontalContinuousChanged;
        public Action OnVerticalPageArrangementChanged;
        public Action OnHorizontalPageArrangementChanged;
    }
}