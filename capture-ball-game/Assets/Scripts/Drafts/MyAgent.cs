using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class MyAgent : CogsAgent
{
    // ------------------MONOBEHAVIOR FUNCTIONS-------------------
    
    // Initialize values
    protected override void Start()
    {
        base.Start();
        AssignBasicRewards();
    }

    // For actual actions in the environment (e.g. movement, shoot laser)
    // that is done continuously
    protected override void FixedUpdate() {
        base.FixedUpdate();
        
        LaserControl();
        // Movement based on DirToGo and RotateDir
        if(!IsFrozen()){
            if (!IsLaserOn()){
                rBody.AddForce(dirToGo * GetMoveSpeed(), ForceMode.VelocityChange);
            }
            transform.Rotate(rotateDir, Time.deltaTime * GetTurnSpeed());
        }
    }


    
    // --------------------AGENT FUNCTIONS-------------------------

    // Get relevant information from the environment to effectively learn behavior
    public override void CollectObservations(VectorSensor sensor)
    {
        // Agent velocity in x and z axis 
        var localVelocity = transform.InverseTransformDirection(rBody.velocity);
        sensor.AddObservation(localVelocity.x);
        sensor.AddObservation(localVelocity.z);

        // Time remaning
        sensor.AddObservation(timer.GetComponent<Timer>().GetTimeRemaning());  

        // Agent's current rotation
        var localRotation = transform.rotation;
        sensor.AddObservation(transform.rotation.y);

        // Agent and home base's position
        sensor.AddObservation(this.transform.localPosition);
        sensor.AddObservation(baseLocation.localPosition);

        // for each target in the environment, add: its position, whether it is being carried,
        // and whether it is in a base
        foreach (GameObject target in targets){
            sensor.AddObservation(target.transform.localPosition);
            sensor.AddObservation(target.GetComponent<Target>().GetCarried());
            sensor.AddObservation(target.GetComponent<Target>().GetInBase());
        }
        
        // Whether the agent is frozen
        sensor.AddObservation(IsFrozen());
    }

    // What to do when an action is received (i.e. when the Brain gives the agent information about possible actions)
    public override void OnActionReceived(float[] act)
    {
        AddReward(-0.0005f);
        int forwardAxis = (int)act[0]; //NN output 0
        int rotateAxis = (int)act[1];
        int shootAxis = (int)act[2]; 
        int goToTargetAxis = (int)act[3]; 
        int goToBaseAxis = (int)act[4];

        // Call movePlayer helper to handle the various cases based on brain output
        movePlayer(forwardAxis, rotateAxis, shootAxis, goToTargetAxis, goToBaseAxis);

    }

    // For manual check of controls 
    public override void Heuristic(float[] actionsOut)
    {
        // Overrides brain output with value based on keyboard input
        // forwardAxis -> [0] 
        // rotateAxis -> [1]
        // shootAxis -> [2]; 
        // goToTargetAxis -> [3]; 
        // goToBaseAxis -> [4];


        var discreteActionsOut = actionsOut;
        discreteActionsOut[0] = 0;
        discreteActionsOut[1] = 0;
        
        if (Input.GetKey(KeyCode.UpArrow))
        {
            discreteActionsOut[0] = 1;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            discreteActionsOut[0] = 2;
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            discreteActionsOut[1] = 2;
        }
        
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            discreteActionsOut[1] = 1;
        }
       

        discreteActionsOut[2] = Input.GetKey(KeyCode.Space) ? 1 : 0;

        discreteActionsOut[3] = Input.GetKey(KeyCode.A) ? 1:0;

        discreteActionsOut[4] = Input.GetKey(KeyCode.S) ? 1:0;
     }



    

    protected override void OnTriggerEnter(Collider collision)
    {
        base.OnTriggerEnter(collision);

        // At home base
        if (collision.gameObject.CompareTag("HomeBase") && collision.gameObject.GetComponent<HomeBase>().team == GetTeam())
        {
            AddReward(GetCarrying() * 0.1f); 
        }
    }

    protected override void OnCollisionEnter(Collision collision) 
    {
        base.OnCollisionEnter(collision);

        // target is not in my base and is not being carried and I am not frozen
        if (collision.gameObject.CompareTag("Target") && collision.gameObject.GetComponent<Target>().GetInBase() != GetTeam() && collision.gameObject.GetComponent<Target>().GetCarried() == 0 && !IsFrozen())
        {
            SetReward(0.5f);
        }

        // if hit wall
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-0.1f);
        }
    }



    //  --------------------------HELPERS---------------------------- 
    
    // Assign reward values to basic actions
    private void AssignBasicRewards() {
        rewardDict = new Dictionary<string, float>();

        rewardDict.Add("frozen", -0.1f);
        rewardDict.Add("shooting-laser", 0f);
        rewardDict.Add("hit-enemy", 0.5f);
        rewardDict.Add("dropped-one-target", 0f);
        rewardDict.Add("dropped-targets", 0f);
    }
    
    // Adjust values used for agent actions based on brain output
    private void movePlayer(int forwardAxis, int rotateAxis, int shootAxis, int goToTargetAxis, int goToBaseAxis)
    {
        dirToGo = Vector3.zero;
        rotateDir = Vector3.zero;
        SetLaser(false);

        Vector3 forward = transform.forward;
        Vector3 backward = -transform.forward;
        Vector3 right = transform.up;
        Vector3 left = -transform.up;

        //fowardAxis: 
            // 0 -> do nothing
            // 1 -> go forward
            // 2 -> go backward
        if (forwardAxis == 0){
            //do nothing
        }
        else if (forwardAxis == 1){
            dirToGo = forward;
        }
        else if (forwardAxis == 2){
            //TODO: Go backward
        }

        //rotateAxis: 
            // 0 -> do nothing
            // 1 -> go forward
            // 2 -> go backward
        if (rotateAxis == 0){
            //do nothing
        }
        //TODO: Implement the other cases for rotateDir


        if (shootAxis == 0){
            //do nothing
        }
        else if (shootAxis == 1){
            SetLaser(true);
        }

        
         switch (goToTargetAxis)
        {
            case 0: break; //do nothing 
            case 1: goToNearestTarget(); break;
        }

         switch (goToBaseAxis)
        {
            case 0: break; //do nothing 
            case 1: goToBase(); break;
        }
    }

    // Go to home base
    private void goToBase(){
        turnAndGo(getYAngleToObject(myBase));
    }

    // Go to the nearest target
    private void goToNearestTarget(){
        GameObject target = getNearestTarget();
        if (target != null){
            float rotation = getYAngleToObject(target);
            turnAndGo(rotation);
        }        
    }

    // Rotate and go in specified direction
    private void turnAndGo(float rotation){

        if(rotation < -5f){
            rotateDir = transform.up;
        }
        else if (rotation > 5f){
            rotateDir = -transform.up;
        }
        else {
            dirToGo = transform.forward;
        }
    }

    // return reference to nearest target
    protected GameObject getNearestTarget(){
        float distance = 200;
        GameObject nearestTarget = null;
        foreach (var target in targets)
        {
            float currentDistance = Vector3.Distance(target.transform.localPosition, transform.localPosition);
            if (currentDistance < distance && target.GetComponent<Target>().GetCarried() == 0 && target.GetComponent<Target>().GetInBase() != team){
                distance = currentDistance;
                nearestTarget = target;
            }
        }
        return nearestTarget;
    }

    // Returns difference between agent rotation and direction to object as a +/- angle
    // (Tells you how to rotate to face a given object)
    private float getYAngleToObject(GameObject obj) {
        
       Vector3 objDir = obj.transform.position - transform.position;
       Vector3 forward = transform.forward;

      float angle = Vector3.SignedAngle(objDir, forward, Vector3.up);
      return angle; 
        
    }
}
