using TermRTS.Algorithms;

namespace TermRTS.Test.Algorithms;

public class ChunkedFovTest
{
    [Fact]
    public void BasicRaycast_empty_grid_includes_cells_within_range()
    {
        var grid = Array.Empty<object>();
        var fov = new Fov();
        fov.BasicRaycast(2, 2, 2, grid, (_, _, _) => false);

        Assert.NotEmpty(fov.VisibleCells);
        Assert.True(fov.VisibleCells.Count >= 5);
        // Origin (2,2) may or may not be included depending on ray angles
        Assert.Contains(new Pos(2, 1), fov.VisibleCells);
        Assert.Contains(new Pos(2, 3), fov.VisibleCells);
        Assert.Contains(new Pos(1, 2), fov.VisibleCells);
        Assert.Contains(new Pos(3, 2), fov.VisibleCells);
    }

    [Fact]
    public void BasicRaycast_wall_blocks_cells_behind()
    {
        var grid = Array.Empty<object>();
        var fov = new Fov();
        // Wall at (2, 3); ray stops at wall and does not add wall or (2, 4)
        fov.BasicRaycast(2, 2, 3, grid, (x, y, _) => x == 2 && y == 3);

        Assert.DoesNotContain(new Pos(2, 4), fov.VisibleCells);
        Assert.NotEmpty(fov.VisibleCells);
    }

    [Fact]
    public void BasicRaycast_range_zero_sees_nothing()
    {
        var grid = Array.Empty<object>();
        var fov = new Fov();
        fov.BasicRaycast(5, 5, 0, grid, (_, _, _) => false);

        Assert.Empty(fov.VisibleCells);
    }

    [Fact]
    public void RecursiveShadowcasting_empty_grid_includes_origin_and_neighbors()
    {
        var grid = Array.Empty<object>();
        var fov = new Fov();
        fov.RecursiveShadowcasting(1, 1, 2, grid, (_, _, _) => false);

        Assert.Contains(new Pos(1, 1), fov.VisibleCells);
        Assert.NotEmpty(fov.VisibleCells);
        Assert.True(fov.VisibleCells.Count >= 5);
    }

    [Fact]
    public void RecursiveShadowcasting_wall_blocks_some_cells()
    {
        var grid = Array.Empty<object>();
        var fov = new Fov();
        // Wall at (1, 0) blocks north; origin (0, 0) still visible
        fov.RecursiveShadowcasting(0, 0, 2, grid, (x, y, _) => x == 1 && y == 0);

        Assert.Contains(new Pos(0, 0), fov.VisibleCells);
        Assert.NotEmpty(fov.VisibleCells);
    }

    [Fact]
    public void RecursiveShadowcasting_clears_previous_result()
    {
        var grid = Array.Empty<object>();
        var fov = new Fov();
        fov.RecursiveShadowcasting(0, 0, 2, grid, (_, _, _) => false);
        fov.RecursiveShadowcasting(5, 5, 1, grid, (_, _, _) => false);

        Assert.DoesNotContain(new Pos(0, 0), fov.VisibleCells);
        Assert.Contains(new Pos(5, 5), fov.VisibleCells);
    }
}