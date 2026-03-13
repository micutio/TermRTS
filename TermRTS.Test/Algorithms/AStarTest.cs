using System.Numerics;
using TermRTS.Algorithms;

namespace TermRTS.Test.Algorithms;

public class AStarTest
{
    private const float Epsilon = 1e-5f;

    private static bool Vector2Near(Vector2 a, Vector2 b)
    {
        return Math.Abs(a.X - b.X) < Epsilon && Math.Abs(a.Y - b.Y) < Epsilon;
    }

    [Fact]
    public void ComputePath_empty_grid_returns_path_from_start_to_goal()
    {
        var start = new Vector2(0, 0);
        var goal = new Vector2(4, 4);
        var astar = new AStar(5, 5, start, goal);

        var path = astar.ComputePath();

        Assert.NotNull(path);
        Assert.True(path.Count >= 2);
        Assert.True(Vector2Near(goal, path[0]));
        Assert.True(Vector2Near(start, path[^1]));
        for (var i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var b = path[i + 1];
            var dx = Math.Abs(a.X - b.X);
            var dy = Math.Abs(a.Y - b.Y);
            Assert.True(Math.Abs(dx + dy - 1f) < Epsilon,
                $"Consecutive path cells must be adjacent: {a} -> {b}");
        }
    }

    [Fact]
    public void ComputePath_start_equals_goal_returns_single_cell_path()
    {
        var pos = new Vector2(2, 2);
        var astar = new AStar(5, 5, pos, pos);

        var path = astar.ComputePath();

        Assert.NotNull(path);
        Assert.Single(path);
        Assert.True(Vector2Near(pos, path[0]));
    }

    [Fact]
    public void ComputePath_goal_unreachable_returns_null()
    {
        var start = new Vector2(0, 0);
        var goal = new Vector2(1, 1);
        var astar = new AStar(3, 3, start, goal)
        {
            // Block the only path: (1,1) is the goal, and we block any edge involving (1,1)
            Weight = (from, to) =>
                to == goal || from == goal ? float.PositiveInfinity : 1f
        };

        var path = astar.ComputePath();

        Assert.Null(path);
    }

    [Fact]
    public void ComputePath_custom_weight_uses_weight_for_cost()
    {
        var start = new Vector2(0, 0);
        var goal = new Vector2(2, 0);
        var astar = new AStar(5, 5, start, goal)
        {
            Weight = (from, to) =>
            {
                // Prefer moving through (1,0) by making (0,1)->(1,1) expensive
                if (from == new Vector2(0, 1) && to == new Vector2(1, 1)) return 100f;
                return 1f;
            }
        };

        var path = astar.ComputePath();

        Assert.NotNull(path);
        Assert.True(Vector2Near(goal, path[0]));
        Assert.True(Vector2Near(start, path[^1]));
    }

    [Fact]
    public void CameFrom_returns_predecessor_after_compute()
    {
        var start = new Vector2(0, 0);
        var goal = new Vector2(1, 1);
        var astar = new AStar(3, 3, start, goal);
        var path = astar.ComputePath();

        Assert.NotNull(path);
        var fromGoal = astar.CameFrom(goal);
        Assert.True(Vector2Near(fromGoal, new Vector2(1, 0)) ||
                    Vector2Near(fromGoal, new Vector2(0, 1)));
        Assert.True(Vector2Near(start, astar.CameFrom(fromGoal)));
    }
}