﻿using System;
using System.Collections;
using UnityEngine;

namespace Svelto.Tasks.Example.MillionPoints.Multithreading
{
    public partial class MillionPointsCPU
    {
        //yes this is running from another thread
        IEnumerator MainLoopOnOtherThread()
        {
            var syncRunner = new SyncRunner();

            var then = DateTime.Now;

            //Let's start the MainThread Loop
            RenderingOnCoroutineRunner().ThreadSafeRun();
            
            var CopyBufferOnUpdateRunner = new SimpleEnumerator(this); //let's avoid useless allocations
            
            //let's avoid allocations inside the loop
            Func<bool> onExternalBreak = OnExternalBreak;

            while (_breakIt == false)
            {
                _time = (float) (DateTime.Now - then).TotalSeconds;
                //Since we are using the SyncRunner, we don't need to yield the execution
                //as the SyncRunner is meant to stall the thread where it starts from.
                //The main thread will be stuck until the multiParallelTask has been
                //executed. A MultiParallelTaskCollection relies on its own
                //internal threads to run, so although the Main thread is stuck
                //the operation will complete
                _multiParallelTasks.ThreadSafeRunOnSchedule(syncRunner);
                //then it resumes here, however the just computed particles 
                //cannot be passed to the compute buffer now,
                //as the Unity methods are not thread safe
                //so I have to run a simple enumerator on the main thread
                var continuator = CopyBufferOnUpdateRunner.ThreadSafeRunOnSchedule(StandardSchedulers.updateScheduler);
                //and I will wait it to complete, still exploting the continuation wrapper.
                //continuators can break on extra conditions too;
                continuator.BreakOnCondition(onExternalBreak);
                //We need to wait the MainThread to finish its operation before to run the 
                //next iteration. So let's stall using the syncrunner;
                continuator.RunOnSchedule(syncRunner);
            }

            //the application is shutting down. This is not that necessary in a 
            //standalone client, but necessary to stop the thread when the 
            //application is stopped in the Editor to stop all the threads.
            _multiParallelTasks.ClearAndKill();

            TaskRunner.Instance.StopAndCleanupAllDefaultSchedulerTasks();

            yield break;
        }

        bool OnExternalBreak()
        {
            return _breakIt;
        }

        IEnumerator RenderingOnCoroutineRunner()
        {
            var bounds = new Bounds(_BoundCenter, _BoundSize);

            while (true)
            {
                //render the particles. I use DrawMeshInstancedIndirect but
                //there aren't any compute shaders running. This is so cool!
                Graphics.DrawMeshInstancedIndirect(_pointMesh, 0, _material,
                    bounds, _GPUInstancingArgsBuffer);

                //continue the cycle on the next frame
                yield return null;
            }
        }
        
        class SimpleEnumerator:IEnumerator
        {
            MillionPointsCPU _million;

            public SimpleEnumerator(MillionPointsCPU million)
            {
                _million = million;
            }
            
            public bool MoveNext()
            {
                _million._particleDataBuffer.SetData(_million._gpuparticleDataArr);

                return false;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public object Current { get; }
        }
    }
}