using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TabuVRP002
{
    public class Point
    {
        public double x;
        public double y;
        public int i;
        public Point(double x, double y)
        {
            this.x = x;
            this.y = y;
            i = -1;
        }
        public Point(double x, double y, int i)
        {
            this.x = x;
            this.y = y;
            this.i = i;
        }
    }
}
