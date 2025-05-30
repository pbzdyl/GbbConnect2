﻿#define SIMULATE_SOC

using GbbEngine2.Configuration;

namespace GbbEngine2.Server
{
    public partial class JobManager
    {

        private CancellationTokenSource cts = new();

        private object StatisticFileLock = new object();

        public void OurStartJobs(Configuration.Parameters Parameters, GbbLibSmall.IOurLog log)
        {
            // load plant state
            foreach (var plant in Parameters.Plants)
            {
                plant.PlantState = PlantState.OurLoadState(plant);
            }

            Task.Run(() => OurMqttService(Parameters, cts.Token, log), cts.Token);

        }


        public void OurStopJobs(Configuration.Parameters Parameters)
        {
            foreach (var plant in Parameters.Plants)
                if (plant.PlantState!=null)
                    plant.PlantState.OurSaveState();

            cts.Cancel();
        }
    }
}
