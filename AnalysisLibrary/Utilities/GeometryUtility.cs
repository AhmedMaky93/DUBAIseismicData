using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TempAnalysis.Models;
using TempAnalysis.OpenseesCommand;

namespace TempAnalysis.Utilities
{
    [DataContract(IsReference = true)]
    [KnownType(typeof(CoreWallLocation))]
    public class Location
    {
        [DataMember]
        public int X;

        [DataMember]
        public int Y;

        public Location()
        {

        }
        public Location(int x, int y)
        {
            X = x;
            Y = y;
        }
        public Location Mirror()
        {
            return new Location(Y,X);
        }
        public Point2D ToPoint(GridSystem grd)
        {
            return new Point2D(grd.GetValue(0, X), grd.GetValue(1, Y));
        }
        public BaseNode ToNode(GridSystem grd, int FloorIndex)
        {
            return grd.AddNode(X, Y, FloorIndex, FloorIndex ==0 ? NodeType.FixedNode: NodeType.BaseNode);
        }
        public override bool Equals(object obj)
        {
            if (obj is Location)
                return this == (obj as Location);
            return false;
        }
        public static bool operator ==(Location p1, Location p2)
        {
            return p1.X == p2.X && p1.Y == p2.Y;
        }
        public static bool operator !=(Location p1, Location p2)
        {
            return !(p1 == p2);
        }
    }
    [DataContract(IsReference = true)]
    public class CoreWallLocation 
    {
        [DataMember]
        public bool Top;
        [DataMember]
        public bool Horizontal;
        public CoreWallLocation()
        {

        }
        public CoreWallLocation(bool Top, bool Horizontal)
        {
            this.Top = Top;
            this.Horizontal = Horizontal;
        }

        public List<Point2D> GetCornerNodes(double OpeningLength)
        {
            double Shift = OpeningLength / 2;
            return GetNodesPoints(OpeningLength, new List<double> { Shift, -Shift });
        }
        public List<Point2D> GetWallsNodes(double OpeningLength, double beamLength)
        {
            double WallLength = (OpeningLength - beamLength) / 2;
            double Shift = (WallLength + beamLength) / 2;
            return GetNodesPoints(OpeningLength, new List<double> { Shift, -Shift });
        }
        public List<Point2D> GetWallInnerLocations(double OpeningLength, double beamLength)
        {
            double Shift = beamLength / 2;
            return GetNodesPoints(OpeningLength, new List<double> { Shift, -Shift });
        }
        public List<List<Point2D>> GetNodesPointsSymmetric(double OpeningLength, List<double> Shifts)
        {
            Point2D Center = GetCenterLocation(OpeningLength);
            List<List<Point2D>> AllPoints = new List<List<Point2D>>();
            AllPoints.Add(GetNodesPoints(OpeningLength,Shifts));
            AllPoints.Add(GetNodesPoints(OpeningLength, Shifts.Select(shift => shift *= -1).Reverse().ToList()));
            return AllPoints;
        }
        protected List<Point2D> GetNodesPoints(double OpeningLength, List<double> Shifts)
        {
            Point2D Center = GetCenterLocation(OpeningLength);
            List<Point2D> points = new List<Point2D>();
            if (Horizontal)
                Shifts.ForEach(Shift => points.Add(new Point2D(Center.X + Shift, Center.Y)));
            else
                Shifts.ForEach(Shift => points.Add(new Point2D(Center.X, Center.Y+ Shift)));
            return points;
        }
        
        protected Point2D GetCenterLocation(double OpeningLength)
        {
            double Shift = (Top? 1:-1) * OpeningLength / 2;
            return Horizontal ? new Point2D(0, Shift) : new Point2D(Shift,0);
        }

        
    }
    [DataContract(IsReference = true)]
    public class Point2D
    {
        [DataMember]
        public double X;
        [DataMember]
        public double Y;
        public Point2D()
        {

        }
        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }
        public Point2D Mirror()
        {
            return new Point2D(Y, X);
        }
        public void Rotate()
        {
            double oldX = X;
            X = -Y;
            Y = oldX;
        }
        public BaseNode ToNode(GridSystem grd, NodeType nodeType, int FloorIndex)
        {
            return grd.AddNode(nodeType,X,Y,grd.GetValue(2,FloorIndex));
        }
        public override string ToString()
        {
            return $"{X} {Y}";
        }
        public Point2D Multiply(double f)
        {
            return new Point2D(X*f, Y*f);
        }
        public static bool IsGeometryEqual(Point2D p1, Point2D p2)
        {
            return (Math.Abs(p1.X - p2.X) < 1E-9) && (Math.Abs(p1.Y - p2.Y) < 1E-9);
        }
        public double DistanceTo(Point2D p)
        {
            return Math.Sqrt(Math.Pow(X - p.X , 2)+ Math.Pow(Y - p.Y, 2));
        }
        public string ToPy(double xf, double yf=1.0)
        {
            return $"{(X * xf).ToString("E5")}, {Y}";
        }
    }

    [DataContract(IsReference = true)]
    public class Line2D
    {
        [DataMember]
        public Point2D P1;
        [DataMember]
        public Point2D P2;
        public Line2D( Point2D P1,Point2D P2)
        {
            this.P1 = P1;
            this.P2 = P2;
        }
        internal bool IntersectWith(Line2D Line,out Point2D point)
        {
            // Line AB represented as a1x + b1y = c1
            double a1 = P2.Y - P1.Y;
            double b1 = P1.X - P2.X;
            double c1 = a1 * (P1.X) + b1 * (P1.Y);

            // Line CD represented as a2x + b2y = c2
            double a2 = Line.P2.Y - Line.P1.Y;
            double b2 = Line.P1.X - Line.P2.X;
            double c2 = a2 * (Line.P1.X) + b2 * (Line.P1.Y);
            double determinant = a1 * b2 - a2 * b1;

            if (determinant == 0)
            {
                // The lines are parallel. This is simplified
                // by returning a pair of FLT_MAX
                point =  new Point2D(double.MaxValue, double.MaxValue);
                return false;
            }
            else
            {
                double x = (b2 * c1 - b1 * c2) / determinant;
                double y = (a1 * c2 - a2 * c1) / determinant;
                point = new Point2D(x, y);
                return true;
            }
        }
        public double GetLength()
        {
            return P1.DistanceTo(P2);
        }
        internal bool IsOnSegment(Point2D p)
        {
            return Math.Abs(GetLength()- p.DistanceTo(P1) - p.DistanceTo(P2)) <1e-9;
        }
    }
}
