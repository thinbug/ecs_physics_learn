using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

[GenerateAuthoringComponent]
public struct TriggerTag : IComponentData
{
}


[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ExportPhysicsWorld))]
[UpdateBefore(typeof(EndFramePhysicsSystem))]
public partial class TriggerEventSystem : SystemBase
{
    BeginInitializationEntityCommandBufferSystem entityCommandBufferSystem;

    StepPhysicsWorld m_StepPhysicsWorldSystem;
    EntityQuery m_TriggerGroup;

    protected override void OnCreate()
    {
        entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();

        m_StepPhysicsWorldSystem = World.GetOrCreateSystem<StepPhysicsWorld>();
        m_TriggerGroup = GetEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(TriggerTag)
            }
        });
    }

    [BurstCompile]
    struct TriggerEventJob : ITriggerEventsJob
    {
        [ReadOnly] public int frame;
        [ReadOnly] public ComponentDataFromEntity<TriggerTag> TriggerGroup;
        public ComponentDataFromEntity<TriggerState> StateGroup;
        public EntityCommandBuffer.ParallelWriter CommandBuffer;


        public void Execute(TriggerEvent triggerEvent)
        {
            Entity entityA = triggerEvent.EntityA;
            Entity entityB = triggerEvent.EntityB;

            bool isBodyATrigger = TriggerGroup.HasComponent(entityA);
            bool isBodyBTrigger = TriggerGroup.HasComponent(entityB);

            // 如果触发器和触发器相撞就返回
            if (isBodyATrigger && isBodyBTrigger)
                return;

            //判断触发器碰撞的目标
            Entity entityTarget = isBodyATrigger ? entityB : entityA;
            if (StateGroup.HasComponent(entityTarget))
            {
                //如果被碰撞的有State，说明已经触发了
                var component = StateGroup[entityTarget];
                component.stay_frame = this.frame;
                StateGroup[entityTarget] = component;

                //Debug.Log("JOB : " +entityTarget.Index + " - stay -" + this.frame);
            }
            else
            {
                CommandBuffer.AddComponent(0, entityTarget, new TriggerState() { enter_frame = this.frame , stay_frame = 0 });

                //Debug.Log(entityTarget.Index+" - enter -"+ this.frame);
            }
            //Debug.Log(entityA.Index + " - " + entityB.Index);

        }

    }

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        this.RegisterPhysicsRuntimeSystemReadOnly();
    }

    protected override void OnUpdate()
    {
        if (m_TriggerGroup.CalculateEntityCount() == 0)
        {
            return;
        }

        int _frame = UnityEngine.Time.frameCount;
        var ecb = entityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
        //Debug.Log("frame1:" + _frame);

        //判断_frame , 这个foreach必须放在下面的job1前面，因为添加TriggerState后就到下一帧了。
        Entities.WithAll<TriggerState>().WithBurst().ForEach((Entity ent, in TriggerState state) =>
        {
            if (state.stay_frame == 0)
            {
                //这些是刚进入的Enter
                UnityEngine.Debug.Log("Enter :" + ent.Index);
            }
            
        }).ScheduleParallel(Dependency).Complete();

        //检测 - 为了进入
        var job1 = new TriggerEventJob
        {
            frame = _frame,
            TriggerGroup = GetComponentDataFromEntity<TriggerTag>(true),
            StateGroup = GetComponentDataFromEntity<TriggerState>(),
            CommandBuffer = ecb
        }.Schedule(m_StepPhysicsWorldSystem.Simulation, Dependency);
        job1.Complete();

        //检测是否stay和exit ， 放在job1的后面是因为需要更新一次stay_frame,然后检测是否和当前_frame一样
        Entities.WithAll<TriggerState>().WithBurst().ForEach((Entity ent, in TriggerState state) =>
        {
            //UnityEngine.Debug.Log("f :" + state.enter_frame + " - " + state.stay_frame + " - " + _frame);
            if (state.stay_frame == _frame)
            {
                //这些是stay
                UnityEngine.Debug.Log($"stay : {ent.Index}");
            }
            else
            {
                //退出的exit
                UnityEngine.Debug.Log($"Exit : {ent.Index}");
                ecb.RemoveComponent<TriggerState>(0, ent);
            }
        }).ScheduleParallel(Dependency).Complete();

        entityCommandBufferSystem.AddJobHandleForProducer(this.Dependency);

    }
}
