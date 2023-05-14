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
        public BaseNode ToNode(GridSystem grd, NodeType nodeType, IDsManager IDM, int FloorIndex)
        {
            return grd.AddNode(X, Y, FloorIndex, nodeType, IDM);
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
        public int VD;
        [DataMember]
        public int A1;
        [DataMember]
        public int A2;
        public CoreWallLocation()
        {

        }
        public CoreWallLocation(int vDI, int A1, int A2)
        {
            this.VD = vDI;
            this.A1 = A1;
            this.A2 = A2;
        }
       
        public List<BaseNode> GetNodesAddDistances(GridSystem grd, NodeType nodeType, IDsManager IDM, int FloorIndex, bool Vertical, List<double> distances, List<double> Verticadistances)
        {
            double ZValue = grd.GetValue(2, FloorIndex);
            List<BaseNode> Nodes = new List<BaseNode>();
            double VDD = Verticadistances[VD]; 
            if (Vertical)
            {
                foreach (double distance in distances)
                {
                    Nodes.Add(grd.AddNode(nodeType, IDM, VDD, distance, ZValue));
                }
            }
            else
            {
                foreach (double distance in distances)
                {
                    Nodes.Add(grd.AddNode(nodeType, IDM, distance, VDD, ZValue));
                }
            }
            return Nodes;
        }
        public List<BaseNode> GetOuterShearWallLocationNodes(GridSystem grd,LayoutUtility layout, NodeType nodeType, IDsManager IDM, int FloorIndex, double shearWallLength, double CouplingBeamLength, bool Vertical)
        {
            return GetNodesAddDistances(grd,nodeType,IDM,FloorIndex,Vertical, layout.GetOuterShearWallsDistances(shearWallLength, CouplingBeamLength), layout.GetOuterShearWallsVerticalDistances());
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
        public BaseNode ToNode(GridSystem grd, NodeType nodeType, IDsManager IDM, int FloorIndex)
        {
            return grd.AddNode(nodeType,IDM,X,Y,grd.GetValue(0,FloorIndex));
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
