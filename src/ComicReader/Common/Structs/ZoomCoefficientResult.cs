using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicReader.Common.Structs
{
    internal class ZoomCoefficientResult
    {
        public double FitWidth;
        public double FitHeight;

        public double Min()
        {
            return Math.Min(FitWidth, FitHeight);
        }

        public double Max()
        {
            return Math.Max(FitWidth, FitHeight);
        }
    }
}
