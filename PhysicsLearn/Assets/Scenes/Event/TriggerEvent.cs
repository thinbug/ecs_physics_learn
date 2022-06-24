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

            // ����������ʹ�������ײ�ͷ���
            if (isBodyATrigger && isBodyBTrigger)
                return;

            //�жϴ�������ײ��Ŀ��
            Entity entityTarget = isBodyATrigger ? entityB : entityA;
            if (StateGroup.HasComponent(entityTarget))
            {
                //�������ײ����State��˵���Ѿ�������
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

        //�ж�_frame , ���foreach������������job1ǰ�棬��Ϊ���TriggerState��͵���һ֡�ˡ�
        Entities.WithAll<TriggerState>().WithBurst().ForEach((Entity ent, in TriggerState state) =>
        {
            if (state.stay_frame == 0)
            {
                //��Щ�Ǹս����Enter
                UnityEngine.Debug.Log("Enter :" + ent.Index);
            }
            
        }).ScheduleParallel(Dependency).Complete();

        //��� - Ϊ�˽���
        var job1 = new TriggerEventJob
        {
            frame = _frame,
            TriggerGroup = GetComponentDataFromEntity<TriggerTag>(true),
            StateGroup = GetComponentDataFromEntity<TriggerState>(),
            CommandBuffer = ecb
        }.Schedule(m_StepPhysicsWorldSystem.Simulation, Dependency);
        job1.Complete();

        //����Ƿ�stay��exit �� ����job1�ĺ�������Ϊ��Ҫ����һ��stay_frame,Ȼ�����Ƿ�͵�ǰ_frameһ��
        Entities.WithAll<TriggerState>().WithBurst().ForEach((Entity ent, in TriggerState state) =>
        {
            //UnityEngine.Debug.Log("f :" + state.enter_frame + " - " + state.stay_frame + " - " + _frame);
            if (state.stay_frame == _frame)
            {
                //��Щ��stay
                UnityEngine.Debug.Log($"stay : {ent.Index}");
            }
            else
            {
                //�˳���exit
                UnityEngine.Debug.Log($"Exit : {ent.Index}");
                ecb.RemoveComponent<TriggerState>(0, ent);
            }
        }).ScheduleParallel(Dependency).Complete();

        entityCommandBufferSystem.AddJobHandleForProducer(this.Dependency);

    }
}
