using Gondwana.Common.Enums;
using Gondwana.Grid;
using System.Drawing;

namespace Gondwana.Coordinates;

public class DiagIsoSquareMatrixCoordinates : IGridCoordinates
{
    public Point GetSrcPxlAtGridPt(GridPointMatrix matrix, PointF gridCoord)
    {
        throw new NotImplementedException();
    }

    public PointF GetGridPtAtPxl(GridPointMatrix matrix, Point pixelPt)
    {
        throw new NotImplementedException();

        #region old VB6 code
        /*
'special calculation for FindGridCoordWithPixel when
'g_renderMode is DIAGONAL_ISOMETRIC_SQUARE_MAP
Private Function FindCoordOnDiagIsoSquareMap(ByVal Layer As Long, _
                                         ByVal XPixel As Single, _
                                         ByVal YPixel As Single) As GridPoint_Long

Dim ptReturn As GridPoint_Long
Dim ptGrid As GridPoint_Long
Dim ptPixelMajor As PixelLocation
Dim ptPixelMinor As PixelLocation
Dim lngMinorSection As MINOR_SECTION
Dim sngIsoSlope As Single

With g_layers
    'step 1: find closest even-numbered column
    ptGrid.Layer = Layer
    ptGrid.X = Int((XPixel / .Layer(Layer).TileWidthFinal) + _
            (.Layer(Layer).FirstX / 2)) * 2
    ptGrid.Y = Int((YPixel / .Layer(Layer).TileHeightFinal) + .Layer(Layer).FirstY)
    
    ptReturn.X = ptGrid.X
    ptReturn.Y = ptGrid.Y
    
    'step 2: find even-numbered column source pixel coordinates ("major" source)
    ptPixelMajor.X = (CSng(ptGrid.X) - .Layer(Layer).FirstX) * _
            (.Layer(Layer).TileWidthFinal / 2)
    ptPixelMajor.Y = (CSng(ptGrid.Y) - .Layer(Layer).FirstY) * _
            .Layer(Layer).TileHeightFinal
    
    'step 3: find "minor" section source (quadrant within "major" section - see enum)
    '   i.e., we are dividing the "major" section into 4 "minor" sections
    If ((XPixel - ptPixelMajor.X) < (CSng(.Layer(Layer).TileWidthFinal) / 2)) Then    'left section
        If ((YPixel - ptPixelMajor.Y) < (CSng(.Layer(Layer).TileHeightFinal) / 2)) Then   'top section
            lngMinorSection = UPPER_LEFT
            ptPixelMinor = ptPixelMajor
        Else
            lngMinorSection = LOWER_LEFT
            ptPixelMinor.X = ptPixelMajor.X
            ptPixelMinor.Y = ptPixelMajor.Y + (CSng(.Layer(Layer).TileHeightFinal) / 2)
        End If
    Else        'right section
        If ((YPixel - ptPixelMajor.Y) < (CSng(.Layer(Layer).TileHeightFinal) / 2)) Then   'top section
            lngMinorSection = UPPER_RIGHT
            ptPixelMinor.X = ptPixelMajor.X + (CSng(.Layer(Layer).TileWidthFinal) / 2)
            ptPixelMinor.Y = ptPixelMajor.Y
        Else
            lngMinorSection = LOWER_RIGHT
            ptPixelMinor.X = ptPixelMajor.X + (CSng(.Layer(Layer).TileWidthFinal) / 2)
            ptPixelMinor.Y = ptPixelMajor.Y + (CSng(.Layer(Layer).TileHeightFinal) / 2)
        End If
    End If
    
    'step 4: determine which side of slope within minor section pixel is on
    '   where m=(y-b)/x  (you do remember your algebra, don't you?)
    '   m is slope, y is height, x is width, and b is y-intercept
    sngIsoSlope = CSng(.Layer(Layer).TileHeightFinal) / CSng(.Layer(Layer).TileWidthFinal)
    
    '0.001 is included to avoid divide-by-0 errors
    Select Case lngMinorSection
        Case MINOR_SECTION.UPPER_LEFT
            sngIsoSlope = -1 * sngIsoSlope
            If (((YPixel - ptPixelMinor.Y) - (CSng(.Layer(Layer).TileHeightFinal) / 2)) / _
                    (XPixel - ptPixelMinor.X + 0.001)) < sngIsoSlope Then
                ptReturn.X = ptReturn.X - 1
                ptReturn.Y = ptReturn.Y - 1
            End If
            
        Case MINOR_SECTION.LOWER_LEFT
            If ((YPixel - ptPixelMinor.Y) / _
                    (XPixel - ptPixelMinor.X + 0.001)) > sngIsoSlope _
                    Then ptReturn.X = ptReturn.X - 1
            
        Case MINOR_SECTION.UPPER_RIGHT
            If ((YPixel - ptPixelMinor.Y) / _
                    (XPixel - ptPixelMinor.X + 0.001)) < sngIsoSlope Then
                ptReturn.X = ptReturn.X + 1
                ptReturn.Y = ptReturn.Y - 1
            End If
            
        Case MINOR_SECTION.LOWER_RIGHT
            sngIsoSlope = -1 * sngIsoSlope
            If (((YPixel - ptPixelMinor.Y) - (CSng(.Layer(Layer).TileHeightFinal) / 2)) / _
                    (XPixel - ptPixelMinor.X + 0.001)) > sngIsoSlope _
                    Then ptReturn.X = ptReturn.X + 1
    End Select
End With

FindCoordOnDiagIsoSquareMap = ptReturn
End Function
        */
        #endregion
    }

    public List<GridPoint> GetGridPtListInPxlRange(GridPointMatrix matrix, Rectangle pixelRange, bool includeOverlaps)
    {
        throw new NotImplementedException();
    }

    public Rectangle GetPxlRangeAtGridPt(Tile tile, bool inclOverlaps)
    {
        throw new NotImplementedException();
    }

    public Rectangle GetPxlRangeAtGridPtList(List<Tile> tileList, bool inclOverlaps)
    {
        throw new NotImplementedException();
    }

    public GridPoint GetAdjGridPt(GridPoint gridPt, CardinalDirections direction)
    {
        throw new NotImplementedException();
    }

    public Point[] GetPolygonPts(Tile tile, bool inclOverlaps)
    {
        throw new NotImplementedException();
    }

    public PointF FindEquivGridCoord(PointF valColRow, int xUpperBound, int yUpperBound)
    {
        throw new NotImplementedException();
    }
}
