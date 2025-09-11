namespace Algorithm;

public class MathematicalModel
{
    // private readonly double gap = 0.0010;
        // double runTimeLimit = 10;

        // public Model(double g, double runtime)
        // {
        //     gap = g;
        //     runTimeLimit = runtime;
        // }

        //public void SolveMathematicalModel(StreamWriter srOutput)
        //{
        //    #region MATHEMATICAL MODEL
        //    try
        //    {
        //        Cplex cplex = new Cplex();
        //        Double bigM = (Program.EndOfPlanningHorizon - Program.Now).TotalMinutes;

        //        #region Create decision variables

        //        Dictionary<string, Dictionary<string, INumVar>> Y = new Dictionary<string, Dictionary<string, INumVar>>();
        //        foreach (var i in Program.Tasks)
        //        {
        //            Y.Add(i.Key, new Dictionary<string, INumVar>());
        //            foreach (var k in Program.Technicians)
        //            {
        //                Y[i.Key].Add(k.Key, cplex.NumVar(0.0, 1.0, NumVarType.Int, String.Format("Y_{0}_{1}",i.Key,k.Key)));
        //            }
        //        }

        //        Dictionary<string, Dictionary<string, INumVar>> S = new Dictionary<string, Dictionary<string, INumVar>>();
        //        foreach (var i in Program.Tasks)
        //        {
        //            S.Add(i.Key, new Dictionary<string, INumVar>());
        //            foreach (var k in Program.Technicians)
        //            {
        //                S[i.Key].Add(k.Key, cplex.NumVar(0.0, bigM, NumVarType.Float, String.Format("S_{0}_{1}", i.Key, k.Key)));
        //            }
        //        }

        //        Dictionary<Location, Dictionary<Location, Dictionary<string, INumVar>>> X = new Dictionary<Location, Dictionary<Location, Dictionary<string, INumVar>>>();
        //        foreach (var i in Program.Locations)
        //        {
        //            X.Add(i.Value, new Dictionary<Location, Dictionary<string, INumVar>>());
        //            foreach (var j in Program.Locations)
        //            {
        //                X[i.Value].Add(j.Value, new Dictionary<string, INumVar>());
        //                foreach (var k in Program.Technicians)
        //                {
        //                    X[i.Value][j.Value].Add(k.Key, cplex.NumVar(0.0, 1.0, NumVarType.Int, String.Format("X_{0}_{1}_{2}", i.Value.LocationKey, j.Value.LocationKey, k.Key)));
        //                }
        //            }
        //        }

        //        Dictionary<string, INumVar> C = new Dictionary<string, INumVar>();
        //        foreach (var i in Program.Tasks)
        //        {
        //            C.Add(i.Key, cplex.NumVar(0.0, bigM, NumVarType.Float, String.Format("C_{0}", i.Key)));
        //        }
        //        Dictionary<string, INumVar> O = new Dictionary<string, INumVar>();
        //        foreach (var i in Program.Tasks)
        //        {
        //            O.Add(i.Key, cplex.NumVar(0.0, 1.0, NumVarType.Int, String.Format("O_{0}", i.Key)));
        //        }
        //        Dictionary<string, INumVar> Z = new Dictionary<string, INumVar>();
        //        foreach (var i in Program.Tasks)
        //        {
        //            Z.Add(i.Key, cplex.NumVar(0.0, 1.0, NumVarType.Int, String.Format("Z_{0}", i.Key)));
        //        }


        //        #endregion


        //        #region Constraints
        //        foreach (var k in Program.Technicians)
        //        {
        //            Location home = Program.Locations[String.Format("S{0}", k.Key)];
        //            ILinearNumExpr c1 = cplex.LinearNumExpr();
        //            foreach (var i in Program.Locations)
        //                if (i.Value.isTaskLocation || i.Value == home)
        //                    c1.AddTerm(1, X[home][i.Value][k.Key]);
        //            cplex.AddEq(c1, 1,"tech_fromHome");

        //            foreach (var i in Program.Tasks)
        //            {
        //                foreach (var j in Program.Locations)
        //                {
        //                    Location iLoc = Program.Locations[String.Format("T{0}", i.Key)];
        //                    if (j.Value.isTechnicianHome && j.Value != home)
        //                    {
        //                        cplex.AddEq(X[iLoc][j.Value][k.Key], 0, "tech_ToAnyOtherHome");
        //                        cplex.AddEq(X[j.Value][iLoc][k.Key], 0, "tech_fromAnyOtherHome_1");
        //                    }
        //                }
        //            }
        //        }
        //        foreach (var k in Program.Technicians)
        //        {
        //            Location home = Program.Locations[String.Format("S{0}", k.Key)];
        //            ILinearNumExpr c2 = cplex.LinearNumExpr();
        //            foreach (var i in Program.Locations)
        //                if (i.Value.isTaskLocation || i.Value == home)
        //                    c2.AddTerm(1, X[i.Value][home][k.Key]);
        //            cplex.AddEq(c2, 1, "tech_toHome");
        //        }
        //        foreach (var i in Program.Tasks)
        //        {
        //            ILinearNumExpr c3 = cplex.LinearNumExpr();
        //            c3.AddTerm(1, O[i.Key]);

        //            foreach (var k in Program.Technicians)
        //                if (Program.TaskDurationsPerTaskPerTechnician[i.Key].ContainsKey(k.Key))
        //                    c3.AddTerm(1, Y[i.Key][k.Key]);
        //            cplex.AddEq(c3, 1, "Outsourced or Insourced");
        //        }
        //        foreach (var i in Program.Tasks)
        //        {
        //            foreach (var k in Program.Technicians)
        //            {
        //                ILinearNumExpr c4 = cplex.LinearNumExpr();

        //                Location iLoc = Program.Locations[String.Format("T{0}", i.Key)];
        //                foreach (var j in Program.Locations)
        //                    if (iLoc != j.Value)
        //                        c4.AddTerm(1, X[iLoc][j.Value][k.Key]);

        //                c4.AddTerm(-1, Y[i.Key][k.Key]);
        //                cplex.AddEq(c4, 0, "Relation Btw X and Y - a" + i.Key + "_tech" + k.Key);
        //            }
        //        }
        //        foreach (var i in Program.Tasks)
        //        {
        //            foreach (var k in Program.Technicians)
        //            {
        //                ILinearNumExpr c4b = cplex.LinearNumExpr();

        //                Location iLoc = Program.Locations[String.Format("T{0}", i.Key)];
        //                foreach (var j in Program.Locations)
        //                    if (iLoc != j.Value)
        //                        c4b.AddTerm(1, X[j.Value][iLoc][k.Key]);

        //                c4b.AddTerm(-1, Y[i.Key][k.Key]);
        //                cplex.AddEq(c4b,0, "Relation Btw X and Y - b_task"+i.Key+"_tech"+k.Key);
        //            }
        //        }
        //        foreach (var i in Program.Tasks)
        //        {
        //            foreach (var j in Program.Tasks)
        //            {
        //                foreach (var k in Program.Technicians)
        //                {
        //                    ILinearNumExpr c4b = cplex.LinearNumExpr();
        //                    c4b.AddTerm(1, Y[i.Key][k.Key]);
        //                    c4b.AddTerm(1, Y[j.Key][k.Key]);
        //                    if (i.Value.Zone != "" && j.Value.Zone != "" & i.Value.Zone != j.Value.Zone)
        //                        cplex.AddLe(c4b, 1, "Zone" + i.Key + "_" + j.Key);
        //                }
        //            }
        //        }

        //        foreach (var i in Program.Locations)
        //        {
        //            foreach (var k in Program.Technicians)
        //            {
        //                Location home = Program.Locations[String.Format("S{0}", k.Key)];
        //                if (i.Value.LocationKey != home.LocationKey)
        //                {
        //                    ILinearNumExpr c5 = cplex.LinearNumExpr();

        //                    c5.AddTerm(1, X[i.Value][i.Value][k.Key]);
        //                    cplex.AddEq(c5, 0, "no self loop");
        //                }
        //            }
        //        }

        //        foreach (var i in Program.Tasks)
        //        {
        //            foreach (var k in Program.Technicians)
        //            {
        //                ILinearNumExpr c6 = cplex.LinearNumExpr();

        //                Location iLoc = Program.Locations[String.Format("T{0}", i.Key)];
        //                foreach (var j in Program.Locations)
        //                    if (iLoc != j.Value)
        //                        c6.AddTerm(1, X[iLoc][j.Value][k.Key]);

        //                foreach (var j in Program.Locations)
        //                    if (iLoc != j.Value)
        //                        c6.AddTerm(-1, X[j.Value][iLoc][k.Key]);
        //                cplex.AddEq(c6, 0, "conservation of flow");
        //            }
        //        }

        //        foreach (var i in Program.Tasks)
        //        {
        //            foreach (var k in Program.Technicians)
        //            {
        //                ILinearNumExpr c7 = cplex.LinearNumExpr();

        //                c7.AddTerm(1, S[i.Key][k.Key]);

        //                c7.AddTerm(-1, C[i.Key]);
        //                cplex.AddLe(c7, 0, "Task completion time");
        //            }
        //        }

        //        foreach (var i in Program.Tasks)
        //        {
        //            foreach (var k in Program.Technicians)
        //            {
        //                ILinearNumExpr c8 = cplex.LinearNumExpr();

        //                c8.AddTerm(1, S[i.Key][k.Key]);

        //                c8.AddTerm(-(Program.EndOfPlanningHorizon-Program.Now).TotalMinutes, Y[i.Key][k.Key]);
        //                cplex.AddLe(c8, 0, "Task completion time by tech");
        //            }
        //        }
        //        foreach (var i in Program.Tasks)
        //        {

        //            foreach (var k in Program.Technicians)
        //            {
        //                if (Program.TaskDurationsPerTaskPerTechnician.ContainsKey(i.Key) && Program.TaskDurationsPerTaskPerTechnician[i.Key].ContainsKey(k.Key))
        //                {
        //                    foreach (var j in Program.Tasks)
        //                    {

        //                        ILinearNumExpr c9 = cplex.LinearNumExpr();
        //                        Location iLoc = Program.Locations[String.Format("T{0}", i.Key)];
        //                        Location jLoc = Program.Locations[String.Format("T{0}", j.Key)];
        //                        c9.AddTerm(1, S[i.Key][k.Key]);

        //                        c9.AddTerm(-1, S[j.Key][k.Key]);

        //                        c9.AddTerm((Program.EndOfPlanningHorizon - Program.Now).TotalMinutes, X[iLoc][jLoc][k.Key]);
        //                        cplex.AddLe(c9, (Program.EndOfPlanningHorizon - Program.Now).TotalMinutes - Program.TaskDurationsPerTaskPerTechnician[i.Key][k.Key].TotalMinutes, "Conseq Task completion times ");


        //                    }
        //                }
        //                else
        //                {
        //                    ILinearNumExpr c12 = cplex.LinearNumExpr();
        //                    c12.AddTerm(1, Y[i.Key][k.Key]);
        //                    cplex.AddEq(c12, 0, "Incompatible technicians for tasks");
        //                }
        //            }

        //        }

        //        foreach (var i in Program.Tasks)
        //        {

        //            ILinearNumExpr c13a = cplex.LinearNumExpr();

        //            c13a.AddTerm(-1, C[i.Key]);
        //            c13a.AddTerm((i.Value.P erredStartTime - Program.Now).TotalMinutes, Z[i.Key]);
        //            cplex.AddLe(c13a, 0, "Task timewindow -a");
        //        }

        //        foreach (var i in Program.Tasks)
        //        {

        //            ILinearNumExpr c13b = cplex.LinearNumExpr();

        //            c13b.AddTerm(1, C[i.Key]);
        //            foreach (var k in Program.Technicians)
        //                if (Program.TaskDurationsPerTaskPerTechnician[i.Key].ContainsKey(k.Key))
        //                    c13b.AddTerm(Program.TaskDurationsPerTaskPerTechnician[i.Key][k.Key].TotalMinutes, Y[i.Key][k.Key]);

        //            c13b.AddTerm((bigM - 1*(i.Value.PreferredEndTime - Program.Now).TotalMinutes), Z[i.Key]);
        //            cplex.AddLe(c13b, bigM, "Task timewindow -b");

        //            ILinearNumExpr c13c = cplex.LinearNumExpr();
        //            c13c.AddTerm(1, Z[i.Key]);
        //            c13c.AddTerm(1, O[i.Key]);
        //            cplex.AddEq(c13c, 1, "Task timewindow -c");
        //        }

        //        foreach (var i in Program.Tasks)
        //        {

        //            ILinearNumExpr c14 = cplex.LinearNumExpr();

        //            c14.AddTerm(1, C[i.Key]);
        //            c14.AddTerm(-1*bigM, O[i.Key]);
        //            cplex.AddGe(c14, 0, "Is Outsourced");
        //        }
        //            #endregion

        //            #region Objective Function

        //            ILinearNumExpr TotalCompletionTime = cplex.LinearNumExpr();
        //        foreach (var i in Program.Tasks)
        //        {
        //            TotalCompletionTime.AddTerm(1, C[i.Key]);
        //        }
        //        //cplex.AddMinimize(TotalCompletionTime);

        //        ILinearNumExpr TotalRevenue = cplex.LinearNumExpr();
        //        foreach (var i in Program.Tasks)
        //        {
        //            foreach (var k in Program.Technicians)
        //                if (Program.TaskRevenuesPerTaskPerTechnician.ContainsKey(i.Key) && Program.TaskRevenuesPerTaskPerTechnician[i.Key].ContainsKey(k.Key))
        //                    TotalRevenue.AddTerm(Program.TaskRevenuesPerTaskPerTechnician[i.Key][k.Key], Y[i.Key][k.Key]);
        //        }
        //        cplex.AddMaximize(TotalRevenue);
        //        #endregion


        //        cplex.SetParam(Cplex.Param.Emphasis.MIP, 3);
        //        cplex.SetParam(Cplex.Param.MIP.Tolerances.MIPGap, 0.001);
        //        cplex.SetParam(Cplex.Param.MIP.Strategy.Dive, 2);
        //        cplex.SetParam(Cplex.Param.MIP.Strategy.Probe, -1);
        //        cplex.SetParam(Cplex.Param.NodeAlgorithm, 4);
        //        cplex.SetParam(Cplex.Param.TimeLimit, runTimeLimit);

        //        cplex.ExportModel("mip.lp");
        //        var stopwatch = new Stopwatch();
        //        stopwatch.Start();
        //        TextWriter TWoutput = File.CreateText("EngineLog.txt");
        //        //cplex.SetOut(TWoutput);
        //        bool r = cplex.Solve();
        //        if (cplex.Solve())
        //        {
        //            stopwatch.Stop();

        //            #region Print X values
        //            System.Console.WriteLine("Task\t is Outsourced \t Technician \t StartTime \t Generated Revenue \t Next Task");
        //            foreach (var i in Program.Tasks)
        //            {
        //                System.Console.Write(i.Key +"\t");
        //                String assingedK = "-1";
        //                String nextTask ="-1";
        //                foreach (var k in Program.Technicians)
        //                {
        //                    int value2 = (int)cplex.GetValue(Y[i.Key][k.Key]);
        //                    if (value2 == 0)
        //                        continue;
        //                    assingedK = k.Key;
        //                    System.Console.Write("0\t"+assingedK + "\t");
        //                    System.Console.Write((int)cplex.GetValue(S[i.Key][k.Key]) + "\t");
        //                    System.Console.Write(Program.TaskRevenuesPerTaskPerTechnician[i.Key][k.Key] + "\t");
        //                    foreach (var j in Program.Locations)
        //                    {
        //                        Location iLoc = Program.Locations[String.Format("T{0}", i.Key)];
        //                        int value = (int)cplex.GetValue(X[iLoc][j.Value][k.Key]);
        //                        if (value > 0)
        //                        {
        //                            nextTask = j.Key;
        //                            System.Console.WriteLine(j.Key);
        //                        }
        //                    }

        //                }
        //                if(assingedK=="-1")
        //                    System.Console.WriteLine("1");
        //            }

        //            #endregion
        //            srOutput.AutoFlush = true;
        //            srOutput.Close();

        //        }
        //        TWoutput.Close();
        //        cplex.End();
        //    }
        //    catch (ILOG.Concert.Exception exc)
        //    {
        //        System.Console.WriteLine("Concert exception caught: " + exc);
        //    }
        //    catch (System.IO.IOException ex)
        //    {
        //        System.Console.WriteLine("IO Error: " + ex);
        //    }
        //    #endregion
        //}
}