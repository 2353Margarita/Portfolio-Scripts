using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//-------------------------------- ПОИСК ПУТИ В ПЛОСКОЙ МАТРИЦЕ
public class MatrixManager : MonoBehaviour
{
    private static MatrixManager instance;
    public static MatrixManager Instance => instance;

    [HideInInspector] public int[][] dynamicMatrix; //матрица динамических объектов, 0 - нет препятствий, < 0 - статическое (неразрушаемое) препятствие, > 0 - id матричного коллайдера динамического объекта
    int cellsX = 0, cellsZ = 0; // проинициализировать соответствующими размерами матрицы!

    static CoordsZX recCrossVector;
    static List<CoordsZX_Counter> recPreDetectMatrix = new List<CoordsZX_Counter>();
    static List<CoordsZX> recPathCoords = new List<CoordsZX>();
    static List<CoordsZX> recPrePath = new List<CoordsZX>();
    static List<CoordsZX> recSubPath = new List<CoordsZX>();
    static CoordsZX recCurrentCoords = new CoordsZX();
    static CoordsZX recStartCoords = new CoordsZX();
    static CoordsZX recFinishCoords = new CoordsZX();
    static CoordsZX recWallV = new CoordsZX();
    static CoordsZX recTemp = new CoordsZX();
    static int rec_iteration = 0;
    static int REC_LIMIT = 500;


    // Поиск пути от start до finish
    public List<Vector3> SearchPath(Vector3 start, Vector3 finish, bool searchShortest, bool castDynamicObjects, int pixelRadius = 0)
    {
        List<CoordsZX> pathA = new List<CoordsZX>();
        List<Vector3> path = new List<Vector3>();
        int i;

        recPathCoords.Clear();
        recPrePath.Clear();
        recSubPath.Clear();
        recPreDetectMatrix.Clear();

        var coordsA = NearestWalkableCoordinates(start, finish);
        var coordsB = NearestWalkableCoordinates(finish, start);
        SearchPath3(coordsA.z, coordsA.x, coordsB.z, coordsB.x, searchShortest, castDynamicObjects); // поиск необработанного пути, путь собирается поклеточно
        if (recPathCoords.Count > 0)
        {
            if (pixelRadius > 0) PathPostProcess_Radius(pixelRadius);// если хотим учитывать радиус объекта (объект больше одной ячейки матрицы)
            PathPostProcess_ReducePoints();// удаляем средние точки на прямых участках пути
            PathPostProcess_StairsRomoving();// удаляем лесенку - оставляем диагональные точки
            PathPostProcess_CuttingCorners3();// срезаем углы
            for (i = 0; i < recPathCoords.Count; i++)
                path.Add(SegmentsMatrix.Instance.PointByPixel(recPathCoords[i])); // матрица сегмента - матрица перехода от пиксельных (клеточных) int координат к метрам Unity float
        }

        return path;
    }

    void PathPostProcess_DetourRemoving()
    {
        recPreDetectMatrix.Clear();
        int i, j, k, k_inc;
        bool bridge = true;
        int bridge_len;

        for (i = 0; i < recPathCoords.Count; i++)
        {
            recPreDetectMatrix.AddCounter(recPathCoords[i], 1, cellsZ, cellsX);
        }

        int max_iters = 55;

        while (bridge && max_iters > 0)
        {
            max_iters--;
            bridge = false;
            for (i = 0; i < recPathCoords.Count; i++)
            {
                bridge = false;
                for (j = recPathCoords.Count - 1; j >= i + 1; j--)
                {
                    bridge = false;
                    if (recPathCoords[i].x == recPathCoords[j].x)
                    {
                        if (recPathCoords[i].z < recPathCoords[j].z)
                        {
                            bridge = true;
                            bridge_len = 0;
                            for (k = recPathCoords[i].z + 1; k < recPathCoords[j].z; k++)
                                if (recPreDetectMatrix.GetValue(k, recPathCoords[i].x) > 0 || dynamicMatrix[k][recPathCoords[i].x] != 0)
                                {
                                    bridge = false;
                                    break;
                                }
                                else bridge_len++;
                            if (bridge_len == 0) bridge = false;

                            if (bridge)
                            {
                                k_inc = recPathCoords[i].z + 1;
                                for (k = i + 1; k < i + 1 + bridge_len; k++)
                                {
                                    recPathCoords[k].x = recPathCoords[i].x;
                                    recPathCoords[k].z = k_inc;
                                    k_inc++;
                                    recPreDetectMatrix.AddCounter(recPathCoords[k], 1, cellsZ, cellsX);
                                }
                                for (k = j - 1; k > i + bridge_len; k--)
                                {
                                    recPreDetectMatrix.AddCounter(recPathCoords[k], -1, cellsZ, cellsX);
                                    recPathCoords.RemoveAt(k);
                                }
                            }
                        }
                        else
                        {
                            bridge = true;
                            bridge_len = 0;
                            for (k = recPathCoords[j].z + 1; k < recPathCoords[i].z; k++)
                                if (recPreDetectMatrix.GetValue(k, recPathCoords[i].x) > 0 || dynamicMatrix[k][recPathCoords[i].x] != 0)
                                {
                                    bridge = false;
                                    break;
                                }
                                else bridge_len++;
                            if (bridge_len == 0) bridge = false;

                            if (bridge)
                            {
                                k_inc = recPathCoords[i].z - 1;
                                for (k = i + 1; k < i + 1 + bridge_len; k++)
                                {
                                    recPathCoords[k].x = recPathCoords[i].x;
                                    recPathCoords[k].z = k_inc;
                                    k_inc--;
                                    recPreDetectMatrix.AddCounter(recPathCoords[k], 1, cellsZ, cellsX);
                                }
                                for (k = j - 1; k > i + bridge_len; k--)
                                {
                                    recPreDetectMatrix.AddCounter(recPathCoords[k], -1, cellsZ, cellsX);
                                    recPathCoords.RemoveAt(k);
                                }
                            }
                        }
                    }
                    if (bridge) break;

                    if (recPathCoords[i].z == recPathCoords[j].z)
                    {
                        if (recPathCoords[i].x < recPathCoords[j].x)
                        {
                            bridge = true;
                            bridge_len = 0;
                            for (k = recPathCoords[i].x + 1; k < recPathCoords[j].x; k++)
                                if (recPreDetectMatrix.GetValue(recPathCoords[i].z, k) > 0 || dynamicMatrix[recPathCoords[i].z][k] > 0)
                                {
                                    bridge = false;
                                    break;
                                }
                                else bridge_len++;
                            if (bridge_len == 0) bridge = false;

                            if (bridge)
                            {
                                k_inc = recPathCoords[i].x + 1;
                                for (k = i + 1; k < i + 1 + bridge_len; k++)
                                {
                                    recPathCoords[k].x = k_inc;
                                    recPathCoords[k].z = recPathCoords[i].z;
                                    k_inc++;
                                    recPreDetectMatrix.AddCounter(recPathCoords[k], 1, cellsZ, cellsX);
                                }
                                for (k = j - 1; k > i + bridge_len; k--)
                                {
                                    recPreDetectMatrix.AddCounter(recPathCoords[k], -1, cellsZ, cellsX);
                                    recPathCoords.RemoveAt(k);
                                }
                            }
                        }
                        else
                        {
                            bridge = true;
                            bridge_len = 0;
                            for (k = recPathCoords[j].x + 1; k < recPathCoords[i].x; k++)
                                if (recPreDetectMatrix.GetValue(recPathCoords[i].z, k) > 0 || dynamicMatrix[recPathCoords[i].z][k] > 0)
                                {
                                    bridge = false;
                                    break;
                                }
                                else bridge_len++;

                            if (bridge_len == 0) bridge = false;
                            if (bridge)
                            {
                                k_inc = recPathCoords[i].x - 1;
                                for (k = i + 1; k < i + 1 + bridge_len; k++)
                                {
                                    recPathCoords[k].x = k_inc;
                                    recPathCoords[k].z = recPathCoords[i].z;
                                    k_inc--;
                                    recPreDetectMatrix.AddCounter(recPathCoords[k], 1, cellsZ, cellsX);
                                }
                                for (k = j - 1; k > i + bridge_len; k--)
                                {
                                    recPreDetectMatrix.AddCounter(recPathCoords[k], -1, cellsZ, cellsX);
                                    recPathCoords.RemoveAt(k);
                                }
                            }
                        }
                    }
                    if (bridge) break;
                }
                if (bridge) break;
            }
        }
    }

    void PathPostProcess_StairsRomoving()
    {
        int i;
        recPreDetectMatrix.Clear();
        for (i = 0; i < recPathCoords.Count; i++)
        {
            recPreDetectMatrix.AddCounter(recPathCoords[i], 1, cellsZ, cellsX);
        }

        for (i = recPathCoords.Count - 3; i >= 0; i--)
        {
            if ((recPathCoords[i + 2] + new CoordsZX(1, 1)).IsEqual(recPathCoords[i]))
            {
                recPathCoords.RemoveAt(i + 1);
                i--;
                continue;
            }

            if ((recPathCoords[i + 2] + new CoordsZX(-1, 1)).IsEqual(recPathCoords[i]))
            {
                recPathCoords.RemoveAt(i + 1);
                i--;
                continue;
            }

            if ((recPathCoords[i + 2] + new CoordsZX(1, -1)).IsEqual(recPathCoords[i]))
            {
                recPathCoords.RemoveAt(i + 1);
                i--;
                continue;
            }

            if ((recPathCoords[i + 2] + new CoordsZX(-1, -1)).IsEqual(recPathCoords[i]))
            {
                recPathCoords.RemoveAt(i + 1);
                i--;
                continue;
            }
        }
    }

    void PathPostProcess_ReducePoints()
    {
        int i;
        List<int> del_list = new List<int>();
        if (recPathCoords.Count <= 2) return;

        for (i = recPathCoords.Count - 2; i >= 1; i--)
        {
            if (recPathCoords[i].x == recPathCoords[i - 1].x && recPathCoords[i].x == recPathCoords[i + 1].x)
            {
                del_list.Add(i);
                continue;
            }
            if (recPathCoords[i].z == recPathCoords[i - 1].z && recPathCoords[i].z == recPathCoords[i + 1].z)
            {
                del_list.Add(i);
                continue;
            }

            if (recPathCoords[i].x + 1 == recPathCoords[i - 1].x && recPathCoords[i].z + 1 == recPathCoords[i - 1].z
                && recPathCoords[i].x + 1 == recPathCoords[i + 1].x && recPathCoords[i].z + 1 == recPathCoords[i + 1].z)
            {
                del_list.Add(i);
                continue;
            }
            if (recPathCoords[i].x - 1 == recPathCoords[i - 1].x && recPathCoords[i].z - 1 == recPathCoords[i - 1].z
                && recPathCoords[i].x - 1 == recPathCoords[i + 1].x && recPathCoords[i].z - 1 == recPathCoords[i + 1].z)
            {
                del_list.Add(i);
                continue;
            }
            if (recPathCoords[i].x - 1 == recPathCoords[i - 1].x && recPathCoords[i].z + 1 == recPathCoords[i - 1].z
                && recPathCoords[i].x - 1 == recPathCoords[i + 1].x && recPathCoords[i].z + 1 == recPathCoords[i + 1].z)
            {
                del_list.Add(i);
                continue;
            }
            if (recPathCoords[i].x + 1 == recPathCoords[i - 1].x && recPathCoords[i].z - 1 == recPathCoords[i - 1].z
                && recPathCoords[i].x + 1 == recPathCoords[i + 1].x && recPathCoords[i].z - 1 == recPathCoords[i + 1].z)
            {
                del_list.Add(i);
                continue;
            }

            if (recPathCoords[i].x + 1 == recPathCoords[i - 1].x && recPathCoords[i].z + 1 == recPathCoords[i - 1].z
                && recPathCoords[i].x - 1 == recPathCoords[i + 1].x && recPathCoords[i].z - 1 == recPathCoords[i + 1].z)
            {
                del_list.Add(i);
                continue;
            }
            if (recPathCoords[i].x - 1 == recPathCoords[i - 1].x && recPathCoords[i].z - 1 == recPathCoords[i - 1].z
                && recPathCoords[i].x + 1 == recPathCoords[i + 1].x && recPathCoords[i].z + 1 == recPathCoords[i + 1].z)
            {
                del_list.Add(i);
                continue;
            }
            if (recPathCoords[i].x - 1 == recPathCoords[i - 1].x && recPathCoords[i].z + 1 == recPathCoords[i - 1].z
                && recPathCoords[i].x + 1 == recPathCoords[i + 1].x && recPathCoords[i].z - 1 == recPathCoords[i + 1].z)
            {
                del_list.Add(i);
                continue;
            }
            if (recPathCoords[i].x + 1 == recPathCoords[i - 1].x && recPathCoords[i].z - 1 == recPathCoords[i - 1].z
                && recPathCoords[i].x - 1 == recPathCoords[i + 1].x && recPathCoords[i].z + 1 == recPathCoords[i + 1].z)
            {
                del_list.Add(i);
                continue;
            }
        }

        for (i = 0; i < del_list.Count; i++)
        {
            recPathCoords.RemoveAt(del_list[i]);
        }
    }

    public bool RaycastObstacles(int z1, int x1, int z2, int x2)
    {
        int dz = z2 - z1;
        int dx = x2 - x1;
        float zz, xx;
        float iz = Mathf.Abs(dz), ix = Mathf.Abs(dx);

        if (iz == 0 && ix == 0) return false;

        if (iz >= ix)
        {
            xx = x1;
            ix = dx / iz;

            zz = z1;
            iz = dz / iz;

            while ((int)zz != z2)
            {
                xx = xx + ix;
                if (dynamicMatrix[(int)zz][(int)xx] != 0) return true;
                zz = zz + iz;
            }
        }
        else
        {
            zz = z1;
            iz = dz / ix;

            xx = x1;
            ix = dx / ix;

            while ((int)xx != x2)
            {
                zz = zz + iz;
                if (dynamicMatrix[(int)zz][(int)xx] != 0) return true;
                xx = xx + ix;
            }
        }
        return false;
    }

    void PathPostProcess_CuttingCorners()
    {
        int i, j, k;
        List<int> del_list = new List<int>();

        for (i = 0; i < recPathCoords.Count; i++)
        {
            if (del_list.Contains(i)) continue;

            for (j = i + 1; j < recPathCoords.Count; j++)
            {
                if (Mathf.Abs(recPathCoords[i].z - recPathCoords[j].z) <= 2) continue;
                if (Mathf.Abs(recPathCoords[i].x - recPathCoords[j].x) <= 2) continue;
                if (!Instance.RaycastObstacles(recPathCoords[i].z, recPathCoords[i].x, recPathCoords[j].z, recPathCoords[j].x))
                {
                    for (k = i + 1; k < j; k++) if (!del_list.Contains(k)) del_list.Add(k);
                }
            }
        }

        for (i = del_list.Count - 1; i >= 0; i--)
        {
            recPathCoords.RemoveAt(del_list[i]);
        }
    }

    void PathPostProcess_CuttingCorners2()
    {
        int i = 0, j, last_cut_index;
        List<int> del_list = new List<int>();

        while (i < recPathCoords.Count)
        {
            last_cut_index = i + 1;
            for (j = i + 2; j < recPathCoords.Count; j++)
            {
                if (!Instance.RaycastObstacles(recPathCoords[i].z, recPathCoords[i].x, recPathCoords[j].z, recPathCoords[j].x))
                {
                    del_list.Add(last_cut_index);
                    last_cut_index = j;
                }
                else break;
            }
            i = last_cut_index;
        }

        for (i = del_list.Count - 1; i >= 0; i--)
        {
            recPathCoords.RemoveAt(del_list[i]);
        }
    }

    void PathPostProcess_CuttingCorners3()
    {
        int i = 0, j, last_cut_index, start_cut_index, finish_cut_index;
        List<int> del_list = new List<int>();

        while (i < recPathCoords.Count)
        {
            last_cut_index = i + 1;
            start_cut_index = finish_cut_index = -1;

            for (j = i + 2; j < recPathCoords.Count; j++)
            {
                if (!Instance.RaycastObstacles(recPathCoords[i].z, recPathCoords[i].x, recPathCoords[j].z, recPathCoords[j].x))
                {
                    if (start_cut_index == -1)
                    {
                        start_cut_index = last_cut_index;
                        finish_cut_index = j;
                    }
                    else finish_cut_index = j;
                }
            }

            if (finish_cut_index >= 0)
            {
                for (j = start_cut_index; j < finish_cut_index; j++)
                    del_list.Add(j);
                i = finish_cut_index;
            }
            else i = last_cut_index;
        }

        for (i = del_list.Count - 1; i >= 0; i--)
        {
            recPathCoords.RemoveAt(del_list[i]);
        }
    }

    void PathPostProcess_Radius(int pixelRadius)
    {
        int i;
        int diameter = pixelRadius * 2;
        int half_radius = pixelRadius / 2;

        for (i = 0; i < recPathCoords.Count; i++)
        {
            var coord = recPathCoords[i];
            if (IsWall_StaticZ(coord.z + 1, coord.z + pixelRadius, coord.x))
            {
                if (!IsWall_StaticZ(coord.z - 1, coord.z - diameter, coord.x))
                {
                    recPathCoords[i].z = coord.z - pixelRadius;
                }
                else
                {
                    if (!IsWall_StaticZ(coord.z - 1, coord.z - pixelRadius, coord.x))
                        recPathCoords[i].z = coord.z - half_radius;
                }
            }
            else
            {
                if (IsWall_StaticZ(coord.z - 1, coord.z - pixelRadius, coord.x))
                {
                    if (!IsWall_StaticZ(coord.z + 1, coord.z + diameter, coord.x))
                    {
                        recPathCoords[i].z = coord.z + pixelRadius;
                    }
                    else
                    {
                        if (!IsWall_StaticZ(coord.z + 1, coord.z + pixelRadius, coord.x))
                            recPathCoords[i].z = coord.z + half_radius;
                    }
                }
            }

            if (IsWall_StaticX(coord.z, coord.x + 1, coord.x + pixelRadius))
            {
                if (!IsWall_StaticX(coord.z, coord.x - 1, coord.x - diameter))
                {
                    recPathCoords[i].x = coord.x - pixelRadius;
                }
                else
                {
                    if (!IsWall_StaticX(coord.z, coord.x - 1, coord.x - pixelRadius))
                        recPathCoords[i].x = coord.x - half_radius;
                }
            }
            else
            {
                if (IsWall_StaticX(coord.z, coord.x - 1, coord.x - pixelRadius))
                {
                    if (!IsWall_StaticX(coord.z, coord.x + 1, coord.x + diameter))
                    {
                        recPathCoords[i].x = coord.x + pixelRadius;
                    }
                    else
                    {
                        if (!IsWall_StaticX(coord.z, coord.x + 1, coord.x + pixelRadius))
                            recPathCoords[i].x = coord.x + half_radius;
                    }
                }
            }
        }
    }

    CoordsZX NearestWalkableCoordinates(Vector3 point, Vector3 pointTarget)
    {
        int i, r;
        int x1, x2, z1, z2;
        CoordsZX coords = new CoordsZX();

        if (!SegmentsMatrix.Instance.PointInMatrix(point))
        {
            point.x = Mathf.Clamp(point.x, SegmentsMatrix.Instance.StartCoordX, SegmentsMatrix.Instance.FinishCoordX);
            point.z = Mathf.Clamp(point.z, SegmentsMatrix.Instance.StartCoordZ, SegmentsMatrix.Instance.FinishCoordZ);
        }

        SegmentsMatrix.Instance.CoordsGlobalMatrix(point, ref coords);
        if (dynamicMatrix[coords.z][coords.x] == 0) return coords;

        int maxR = 20;
        for (r = 1; r < maxR; r++)
        {
            if (coords.x - r < 0) x1 = coords.x;
            else x1 = coords.x - r;
            if (coords.x + r >= cellsX) x2 = coords.x;
            else x2 = coords.x + r;

            if (coords.z - r < 0) z1 = coords.z;
            else z1 = coords.z - r;
            if (coords.z + r >= cellsZ) z2 = coords.z;
            else z2 = coords.z + r;

            for (i = x1; i <= x2; i++)
            {
                if (dynamicMatrix[z1][i] == 0) return new CoordsZX(z1, i);
                if (dynamicMatrix[z2][i] == 0) return new CoordsZX(z2, i);
            }
            for (i = z1; i <= z2; i++)
            {
                if (dynamicMatrix[i][x1] == 0) return new CoordsZX(i, x1);
                if (dynamicMatrix[i][x2] == 0) return new CoordsZX(i, x2);
            }
        }

        return new CoordsZX(-1, -1);
    }

    void SearchPath3(int z1, int x1, int z2, int x2, bool searchShortest, bool castDynamicObjects)
    {
        int dz = z2 - z1;
        int dx = x2 - x1;
        float zz, xx;
        float iz = Mathf.Abs(dz), ix = Mathf.Abs(dx);

        recPrePath.Clear();
        recCurrentCoords = new CoordsZX();
        recStartCoords = new CoordsZX();
        recFinishCoords = new CoordsZX();

        if (iz == 0 && ix == 0) return;

        recPrePath.Add(new CoordsZX(z1, x1));

        if (iz >= ix)
        {
            xx = x1;
            ix = dx / iz;

            zz = z1;
            iz = dz / iz;

            while ((int)zz != z2)
            {
                xx = xx + ix;
                recPrePath.Add(new CoordsZX((int)zz, (int)xx));
                zz = zz + iz;
            }
        }
        else
        {
            zz = z1;
            iz = dz / ix;

            xx = x1;
            ix = dx / ix;

            while ((int)xx != x2)
            {
                zz = zz + iz;
                recPrePath.Add(new CoordsZX((int)zz, (int)xx));
                xx = xx + ix;
            }
        }
        recPrePath.Add(new CoordsZX(z2, x2));

        int i = 0, j;
        List<CoordsZX> pathA = new List<CoordsZX>();

        while (i < recPrePath.Count)
        {
            if ((castDynamicObjects && dynamicMatrix[recPrePath[i].z][recPrePath[i].x] != 0)
                || (!castDynamicObjects && dynamicMatrix[recPrePath[i].z][recPrePath[i].x] > 0))
            {
                recSubPath.Clear();
                recSubPath.Add(new CoordsZX(recPrePath[i - 1]));
                recStartCoords.Set(recPrePath[i - 1]);

                i++;
                while (((castDynamicObjects && dynamicMatrix[recPrePath[i].z][recPrePath[i].x] != 0)
                    || (!castDynamicObjects && dynamicMatrix[recPrePath[i].z][recPrePath[i].x] > 0))
                    && i < recPrePath.Count)
                {
                    i++;
                }

                if (i < recPrePath.Count)
                {
                    recFinishCoords.Set(recPrePath[i]);
                    recCrossVector = (recFinishCoords - recStartCoords).ToCrossVector();
                    recCurrentCoords.Set(recStartCoords);
                    recWallV.Set(0, 0);
                    recWallV = (recFinishCoords - recStartCoords).ToCrossVectorMin();
                    //Debug.LogError("SUBPATH 1");
                    rec_iteration = 0;
                    if (castDynamicObjects) SearchSubPath();
                    else SearchSubPath_StaticObjects();

                    if (recSubPath.Count == 0 || searchShortest)
                    {
                        if (searchShortest)
                        {
                            pathA.Clear();
                            pathA.AddRange(recSubPath);
                        }

                        recSubPath.Clear();
                        recSubPath.Add(new CoordsZX(recFinishCoords));
                        recFinishCoords.Set(recStartCoords);
                        recStartCoords.Set(recSubPath[0]);
                        recCrossVector = (recFinishCoords - recStartCoords).ToCrossVector();
                        recCurrentCoords.Set(recStartCoords);
                        recWallV = (recFinishCoords - recStartCoords).ToCrossVectorMin();
                        //recWallV.Set(0, 0);
                        //Debug.LogError("SUBPATH 2");
                        rec_iteration = 0;
                        if (castDynamicObjects) SearchSubPath();
                        else SearchSubPath_StaticObjects();

                        if (searchShortest && pathA.Count < recSubPath.Count)
                        {
                            for (j = 1; j < pathA.Count; j++)
                            {
                                recPathCoords.Add(pathA[j]);
                            }
                        }
                        else
                        {
                            for (j = recSubPath.Count - 2; j >= 0; j--)
                            {
                                recPathCoords.Add(recSubPath[j]);
                            }
                        }
                    }
                    else
                    {
                        for (j = 1; j < recSubPath.Count; j++)
                        {
                            recPathCoords.Add(recSubPath[j]);
                        }
                    }
                }
            }
            else
            {
                recPathCoords.Add(recPrePath[i]);
                i++;
            }
        }
    }

    bool IsWall(ref CoordsZX coords)
    {
        if (coords.x < 0 || coords.x >= cellsX
            || coords.z < 0 || coords.z >= cellsZ
            || dynamicMatrix[coords.z][coords.x] != 0)
        {
            return true;
        }
        else return false;
    }

    bool IsWall(int zz, int xx)
    {
        if (xx < 0 || xx >= cellsX
            || zz < 0 || zz >= cellsZ
            || dynamicMatrix[zz][xx] != 0)
            return true;
        else return false;
    }

    bool IsWall_Static(ref CoordsZX coords)
    {
        if (coords.x < 0 || coords.x >= cellsX
            || coords.z < 0 || coords.z >= cellsZ
            || dynamicMatrix[coords.z][coords.x] < 0)
        {
            return true;
        }
        else return false;
    }

    bool IsWall_Static(int zz, int xx)
    {
        if (xx < 0 || xx >= cellsX
            || zz < 0 || zz >= cellsZ
            || dynamicMatrix[zz][xx] < 0)
            return true;
        else return false;
    }

    bool IsWall_StaticZ(int z_start, int z_finish, int xx)
    {
        int zz = z_start;
        int step = System.Math.Sign(z_finish - z_start);

        while (zz != z_finish)
        {
            if (xx < 0 || xx >= cellsX || zz < 0 || zz >= cellsZ || dynamicMatrix[zz][xx] < 0)
                return true;
            zz += step;
        }
        return false;
    }

    bool IsWall_StaticX(int zz, int x_start, int x_finish)
    {
        int xx = x_start;
        int step = System.Math.Sign(x_finish - x_start);

        while (xx != x_finish)
        {
            if (xx < 0 || xx >= cellsX || zz < 0 || zz >= cellsZ || dynamicMatrix[zz][xx] < 0)
                return true;
            xx += step;
        }
        return false;
    }

    void SearchSubPath()
    {
        rec_iteration++;
        if (rec_iteration >= REC_LIMIT)
        {
            recSubPath.Clear();
            return;
        }

        recTemp.Set(recCurrentCoords.z + recCrossVector.z, recCurrentCoords.x + recCrossVector.x);

        if (IsWall(ref recTemp))
        {
            if (IsWall(recCurrentCoords.z + recWallV.z, recCurrentCoords.x + recWallV.x))
            {
                recTemp.Set(recCrossVector);
                recCrossVector.Set(-recWallV.z, -recWallV.x);
                recWallV.Set(recTemp);
                SearchSubPath();
                return;
            }
            else
            {
                recTemp.Set(recCrossVector);
                recCrossVector.Set(recWallV);
                recWallV.Set(recTemp);
                SearchSubPath();
                return;
            }
        }
        else
        {
            if (recTemp.IsEqual(recFinishCoords))
            {
                recSubPath.Add(new CoordsZX(recTemp));
                return;
            }

            if (IsWall(recTemp.z + recWallV.z, recTemp.x + recWallV.x))
            {
                recPathCoords.Add(new CoordsZX(recTemp));
                recCurrentCoords.Set(recTemp);
                SearchSubPath();
                return;
            }
            else
            {
                recPathCoords.Add(new CoordsZX(recTemp));
                recCurrentCoords.Set(recTemp);
                recTemp.Set(recCrossVector);
                recCrossVector.Set(recWallV);
                recWallV.Set(-recTemp.z, -recTemp.x);
                SearchSubPath();
                return;
            }
        }
    }

    void SearchSubPath_StaticObjects()
    {
        rec_iteration++;
        if (rec_iteration >= REC_LIMIT)
        {
            recSubPath.Clear();
            return;
        }

        recTemp.Set(recCurrentCoords.z + recCrossVector.z, recCurrentCoords.x + recCrossVector.x);

        if (IsWall_Static(ref recTemp))
        {
            if (IsWall_Static(recCurrentCoords.z + recWallV.z, recCurrentCoords.x + recWallV.x))
            {
                recTemp.Set(recCrossVector);
                recCrossVector.Set(-recWallV.z, -recWallV.x);
                recWallV.Set(recTemp);
                SearchSubPath();
                return;
            }
            else
            {
                recTemp.Set(recCrossVector);
                recCrossVector.Set(recWallV);
                recWallV.Set(recTemp);
                SearchSubPath();
                return;
            }
        }
        else
        {
            if (recTemp.IsEqual(recFinishCoords))
            {
                recSubPath.Add(new CoordsZX(recTemp));
                return;
            }

            if (IsWall_Static(recTemp.z + recWallV.z, recTemp.x + recWallV.x))
            {
                recPathCoords.Add(new CoordsZX(recTemp));
                recCurrentCoords.Set(recTemp);
                SearchSubPath();
                return;
            }
            else
            {
                recPathCoords.Add(new CoordsZX(recTemp));
                recCurrentCoords.Set(recTemp);
                recTemp.Set(recCrossVector);
                recCrossVector.Set(recWallV);
                recWallV.Set(-recTemp.z, -recTemp.x);
                SearchSubPath();
                return;
            }
        }
    }
}

[System.Serializable]
public class CoordsZX
{
    public int z;
    public int x;

    public CoordsZX()
    {
        z = 0;
        x = 0;
    }

    public CoordsZX(int _z, int _x)
    {
        z = _z;
        x = _x;
    }

    public CoordsZX(CoordsZX v)
    {
        z = v.z;
        x = v.x;
    }

    public bool IsEqual(CoordsZX other)
    {
        return z == other.z && x == other.x;
    }

    public bool IsEqual(float _z, float _x)
    {
        return z == _z && x == _x;
    }

    public bool IsZero()
    {
        return z == 0 && x == 0;
    }

    public void Set(CoordsZX v)
    {
        z = v.z;
        x = v.x;
    }

    public void Set(int vz, int vx)
    {
        z = vz;
        x = vx;
    }

    public override string ToString()
    {
        return DebugString();
    }

    public string DebugString()
    {
        return "(" + z.ToString() + ";" + x.ToString() + ")";
    }

    public static CoordsZX operator +(CoordsZX a, CoordsZX b)
    {
        CoordsZX c = new CoordsZX();
        c.x = a.x + b.x;
        c.z = a.z + b.z;
        return c;
    }

    public static CoordsZX operator +(CoordsZX a, Vector2Int b)
    {
        CoordsZX c = new CoordsZX();
        c.x = a.x + b.x;
        c.z = a.z + b.y;
        return c;
    }

    public static CoordsZX operator -(CoordsZX a, CoordsZX b)
    {
        CoordsZX c = new CoordsZX();
        c.x = a.x - b.x;
        c.z = a.z - b.z;
        return c;
    }

    public static CoordsZX operator -(CoordsZX a, Vector2Int b)
    {
        CoordsZX c = new CoordsZX();
        c.x = a.x - b.x;
        c.z = a.z - b.y;
        return c;
    }
}

public static class SubMatrix_Extension
{
    public static bool ContainsCoords(this List<CoordsZX> list, float z, float x)
    {
        foreach (var data in list)
        {
            if (data.z == z && data.x == x)
                return true;
        }
        return false;
    }

    public static bool ContainsCoords(this List<CoordsZX> list, CoordsZX coords)
    {
        foreach (var data in list)
        {
            if (data.z == coords.z && data.x == coords.x)
                return true;
        }
        return false;
    }

    public static CoordsZX ToCrossVector(this Vector3 direction3D)
    {
        CoordsZX result = new CoordsZX();

        if (Mathf.Abs(direction3D.x) > Mathf.Abs(direction3D.z))
        {
            result.x = System.Math.Sign(direction3D.x);
            result.z = 0;
        }
        else
        {
            result.x = 0;
            result.z = System.Math.Sign(direction3D.z);
        }
        return result;
    }

    public static CoordsZX ToCrossVector(this CoordsZX vector)
    {
        CoordsZX result = new CoordsZX();

        if (Mathf.Abs(vector.x) > Mathf.Abs(vector.z))
        {
            result.x = System.Math.Sign(vector.x);
            result.z = 0;
        }
        else
        {
            result.x = 0;
            result.z = System.Math.Sign(vector.z);
        }
        return result;
    }

    public static CoordsZX ToCrossVectorMin(this CoordsZX vector)
    {
        CoordsZX result = new CoordsZX();

        if (Mathf.Abs(vector.x) < Mathf.Abs(vector.z))
        {
            result.x = System.Math.Sign(vector.x);
            result.z = 0;
        }
        else
        {
            result.x = 0;
            result.z = System.Math.Sign(vector.z);
        }
        return result;
    }
}

[System.Serializable]
public class CoordsZX_Counter
{
    public CoordsZX coords;
    public int counter = 0;


    public CoordsZX_Counter(CoordsZX _coords, int _counter)
    {
        coords = new CoordsZX(_coords.z, _coords.x);
        //coords = _coords;
        counter = _counter;
    }
}

public static class CoordsZX_Counter_Extension
{
    public static bool ContainsCoords(this List<CoordsZX_Counter> list, CoordsZX coords)
    {
        foreach (var data in list)
        {
            if (data.coords.IsEqual(coords))
                return true;
        }
        return false;
    }

    public static int GetValue(this List<CoordsZX_Counter> list, CoordsZX coords)
    {
        foreach (var data in list)
        {
            if (data.coords.IsEqual(coords))
                return data.counter;
        }
        return 0;
    }

    public static int GetValue(this List<CoordsZX_Counter> list, int z, int x)
    {
        foreach (var data in list)
        {
            if (data.coords.IsEqual(z, x))
                return data.counter;
        }
        return 0;
    }

    public static bool AddCounter(this List<CoordsZX_Counter> list, CoordsZX coords, int addValue, int cellsZ, int cellsX)
    {
        int i;
        for (i = 0; i < list.Count; i++)
        {
            if (list[i].coords.IsEqual(coords))
            {
                list[i].counter += addValue;
                return true;
            }
        }

        if (coords.x >= 0 && coords.x < cellsX && coords.z >= 0 && coords.z < cellsZ)
        {
            list.Add(new CoordsZX_Counter(coords, addValue));
            return true;
        }
        return false;
    }

    public static bool AddCounter(this List<CoordsZX_Counter> list, int z, int x, int addValue, int cellsZ, int cellsX)
    {
        int i;
        for (i = 0; i < list.Count; i++)
        {
            if (list[i].coords.IsEqual(z, x))
            {
                list[i].counter += addValue;
                return true;
            }
        }

        if (x >= 0 && x < cellsX && z >= 0 && z < cellsZ)
        {
            list.Add(new CoordsZX_Counter(new CoordsZX(z, x), addValue));
            return true;
        }
        return false;
    }
}