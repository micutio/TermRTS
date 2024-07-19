namespace TermRTS.Examples.Circuitry
{
    internal readonly struct Location(int x, int y)
    {
        internal int X { get; } = x;
        internal int Y { get; } = y;
    }

    internal class Node
    {
        public Location Location { get; private set; }
        public bool IsWalkable { get; set; }
        public float G { get; private set; }
        public float H { get; private set; }
        public float F => G + H;
        public NodeState State { get; set; }
        public Node ParentNode { get; set; }
    }

    internal enum NodeState { Untested, Open, Closed }

    /// <summary>
    /// Implementation of the A* path finding algorithm for 2d-grids.
    /// </summary>
    internal class Astar
    {
        private readonly Location _startLocation; // TODO: Replace tuple with immutable struct
        private readonly Location _endLocation;
        private readonly int _width;
        private readonly int _height;
        private readonly bool[,] _map;

        internal Astar(Location startLocation, Location endLocation, int width, int _height, bool[,] map)
        {
            _startLocation = startLocation;
            _endLocation = endLocation;
            _map = map;
        }

        internal bool Search(Node currentNode)
        {
            currentNode.State = NodeState.Closed;
            List<Node> nextNodes = GetAdjacentWalkableNodes(currentNode);
            nextNodes.Sort((n1, n2) => n1.F.CompareTo(n2.F));
            foreach (var nextNode in nextNodes)
            {
                if (nextNode.Location == _endNode.Location)
                {
                    return true;
                }
                else
                {
                    if (Search(nextNode))
                        return true;
                }
            }
            return false;
        }

        private List<Node> GetAdjacentWalkableNodes(Node fromNode)
        {
            var walkableNodes = new List<Node>();
            IEnumerable<Location> nextLocations = GetAdjacentLocations(fromNode.Location);
            foreach (var location in nextLocations)
            {
                var x = location.X;
                var y = location.Y;

                // Stay within the grid's boundaries
                if (x < 0 || x >= this._width || y < 0 || y >= this._height)
                    continue;

                var node = this._nodes[x, y];
                // Ignore non-walkable and closed nodes
                if (!node.IsWalkable || node.State == NodeState.Closed)
                    continue;

                // Already open nodes are only added to the list if their G-Value is lower going
                // via this route
                if (node.State == NodeState.Open)
                {
                    var traversalCost = Node.GetTraversalCost(node.Location, node.ParentNode.Location);
                    var gTemp = fromNode.G + traversalCost;
                    if (gTemp < node.G)
                    {
                        node.ParentNode = fromNode;
                        walkableNodes.Add(node);
                    }
                }
                else
                {
                    // If it's untested, set the parent and flag it as 'Open' for consideration.
                    node.ParentNode = fromNode;
                    node.State = NodeState.Open;
                    walkableNodes.Add(node);
                }
            }

            return walkableNodes;
        }

        IEnumerable<Location> GetAdjacentLocations(Location location)
        {
            var adjacentLocations = new List<Location>
            {
                new Location(location.X + 1, location.Y),
                new Location(location.X, location.Y + 1),
                new Location(location.X - 1, location.Y),
                new Location(location.X, location.Y - 1)
            };

            return adjacentLocations;
        }

        internal List<Location> findPath()
        {
            var path = new List<Location>();
            var success = Search(_startNode);

            if (!success)
                return path;

            var node = _endNode;
            while (node.ParentNode != null)
            {
                path.Add(node.Location);
                node = node.ParentNode;
            }
            path.Reverse();

            return path;
        }
    }
}
