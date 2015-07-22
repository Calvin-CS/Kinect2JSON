(function(ext) {
    
    var firstTime = true;
    var boolean = true;
    var jsonObject = null;
    var xScale = 280;
    var yScale = 210;
    var status = 1;
    
    alert("BEFORE CLICKING OK: Make sure you have have followed the instructions in Kinect2Scratch");
    console.log("Right after the alert");
    
    var wsImpl = window.WebSocket || window.MozWebSocket;
     
    console.log("connecting to server ..");

    // create a new websocket and connect
    window.ws = new wsImpl('ws://localhost:8181/');

    // when data is comming from the server, this metod is called
    ws.onmessage = function (evt) {
        jsonObject = JSON.parse(evt.data);
            if(jsonObject.bodies == '')
            {
                status = 1;
            } else
            {
                status = 2;
            }
    };

    // when the connection is established, this method is called
    ws.onopen = function () {
        console.log('.. connection open');
    };

    // when the connection is closed, this method is called
    ws.onclose = function () {
        console.log('.. connection closed');
        status = 0;
    };

    // Cleanup function when the extension is unloaded
    ext._shutdown = function() {};
    

    // Status reporting code
    // Use this to report missing hardware, plugin or unsupported browser
    ext._getStatus = function() {
        if(status == 0)
        {
            return {status: 0, msg: 'Kinect is not connected to Scratch'};
            //polling function for auto-reconnect should go here
        }
        if(status == 1)
        {
            return {status: 1, msg: 'Kinect is connected, but is not detecting any bodies'};
        }
        if(status == 2)
        {
            return {status: 2, msg: 'Kinect is sending body data'};
        }
        
    };
    
        // Block and block menu descriptions
    var descriptor = {
        blocks: [
            ['', 'My First Block', 'my_first_block'],
            ['r', '%n ^ %n', 'power', 2, 3],
            ['r', '%m.k body 1 sensor value', 'k', 'Head X'],
            ['r', '%m.k body 2 sensor value', 'k1', 'Head X'],
            ['', 'restart connection', 'restart'],
            ['', 'test block', 'test_block'],
            ['b', 'connected', 'connected'],
            ['b', 'body tracked', 'tracked'],
            ['', 'console.log %n', 'write'],
            ['', 'bad only %n', 'writeB'],
            ['r', '%m.l id', 'l', 'Body 1']
        ],
        
        menus: {
	    k: ['Left Ankle X', 'Left Ankle Y', 'Right Ankle X', 'Right Ankle Y', 'Left Elbow X', 'Left Elbow Y', 'Right Elbow X', 'Right Elbow Y', 'Left Foot X', 'Left Foot Y', 'Right Foot X', 'Right Foot Y', 'Left Hand X', 'Left Hand Y', 'Right Hand X', 'Right Hand Y', 'Left Hand Tip X', 'Left Hand Tip Y', 'Right Hand Tip X', 'Right Hand Tip Y', 'Head X', 'Head Y', 'Left Hip X', 'Left Hip Y', 'Right Hip X', 'Right Hip Y', 'Left Knee X', 'Left Knee Y', 'Right Knee X', 'Right Knee Y', 'Neck X', 'Neck Y', 'Left Shoulder X', 'Left Shoulder Y', 'Right Shoulder X', 'Right Shoulder Y', 'Spine Base X', 'Spine Base Y', 'Spine Middle X', 'Spine Middle Y', 'Spine Shoulder X', 'Spine Shoulder Y', 'Left Thumb X', 'Left Thumb Y', 'Right Thumb X', 'Right Thumb Y', 'Left Wrist X', 'Left Wrist Y', 'Right Wrist X', 'Right Wrist Y'],
        l: ['Body 1', 'Body 2']
    }
    };

	ext.my_first_block = function() {
        console.log("My first block");
    };
        
    ext.restart = function() {
        console.log("connecting to server ..");
        window.ws = new wsImpl('ws://localhost:8181/');
    };
	
    ext.power = function(base, exponent) {
        return Math.pow(base, exponent);
    };
    
    ext.test_block = function() {
        console.log(jsonObject.bodies[0].joints[3].x*xScale);
    };
    
        ext.connected = function()
    {
        if(status == 0){
            return false;
        }
        
        if(status == 1 || 2){
            return true;
        }
    };
    
    ext.tracked = function()
    {
        if(status == (0 || 1)){
            return false;
        }
        
        if(status == 2){
            return true;
        }
    };
    
    ext.write = function(m){
        console.log(m);
    };
    
    ext.writeB = function(m){
        if(m == 0)
        {
            console.log("bad");
        }
    };
    
    ext.l = function(m)
    {
        switch(m){
            case 'Body 1': return jsonObject.bodies[0].id;
            case 'Body 2': return jsonObject.bodies[1].id;
        }
    }

    ext.k1 = function(m) {
        switch(m){
            case 'Left Ankle X': return jsonObject.bodies[1].joints[14].x*xScale;
            case 'Left Ankle Y': return jsonObject.bodies[1].joints[14].y*yScale;
            case 'Right Ankle X': return jsonObject.bodies[1].joints[18].x*xScale;
            case 'Right Ankle Y': return jsonObject.bodies[1].joints[18].y*yScale;
            case 'Left Elbow X': return jsonObject.bodies[1].joints[5].x*xScale;
            case 'Left Elbow Y': return jsonObject.bodies[1].joints[5].y*yScale;
            case 'Right Elbow X': return jsonObject.bodies[1].joints[9].x*xScale;
            case 'Right Elbow Y': return jsonObject.bodies[1].joints[9].y*yScale;
            case 'Left Foot X': return jsonObject.bodies[1].joints[15].x*xScale;
            case 'Left Foot Y': return jsonObject.bodies[1].joints[15].y*yScale;
            case 'Right Foot X': return jsonObject.bodies[1].joints[19].x*xScale;
            case 'Right Foot Y': return jsonObject.bodies[1].joints[19].y*yScale;
            case 'Left Hand X': return jsonObject.bodies[1].joints[7].x*xScale;
            case 'Left Hand Y': return jsonObject.bodies[1].joints[7].y*yScale;
            case 'Right Hand X': return jsonObject.bodies[1].joints[11].x*xScale;
            case 'Right Hand Y': return jsonObject.bodies[1].joints[11].y*yScale;
            case 'Left Hand Tip X': return jsonObject.bodies[1].joints[21].x*xScale;
            case 'Left Hand Tip Y': return jsonObject.bodies[1].joints[21].y*yScale;
            case 'Right Hand Tip X': return jsonObject.bodies[1].joints[23].x*xScale;
            case 'Right Hand Tip Y': return jsonObject.bodies[1].joints[23].y*yScale;
            case 'Head X': return jsonObject.bodies[1].joints[3].x*xScale;
            case 'Head Y': return jsonObject.bodies[1].joints[3].y*yScale;
            case 'Left Hip X': return jsonObject.bodies[1].joints[12].x*xScale;
            case 'Left Hip Y': return jsonObject.bodies[1].joints[12].y*yScale;
            case 'Right Hip X': return jsonObject.bodies[1].joints[16].x*xScale;
            case 'Right Hip Y': return jsonObject.bodies[1].joints[16].y*yScale;
            case 'Left Knee X': return jsonObject.bodies[1].joints[13].x*xScale;
            case 'Left Knee Y': return jsonObject.bodies[1].joints[13].y*yScale;
            case 'Right Knee X': return jsonObject.bodies[1].joints[17].x*xScale;
            case 'Right Knee Y': return jsonObject.bodies[1].joints[17].y*yScale;
            case 'Neck X': return jsonObject.bodies[1].joints[2].x*xScale;
            case 'Neck Y': return jsonObject.bodies[1].joints[2].y*yScale;
            case 'Left Shoulder X': return jsonObject.bodies[1].joints[4].x*xScale;
            case 'Left Shoulder Y': return jsonObject.bodies[1].joints[4].y*yScale;
            case 'Right Shoulder X': return jsonObject.bodies[1].joints[8].x*xScale;
            case 'Right Shoulder Y': return jsonObject.bodies[1].joints[8].y*yScale;
            case 'Spine Base X': return jsonObject.bodies[1].joints[1].x*xScale;
            case 'Spine Base Y': return jsonObject.bodies[1].joints[1].y*yScale;
            case 'Spine Middle X': return jsonObject.bodies[1].joints[1].x*xScale;
            case 'Spine Middle Y': return jsonObject.bodies[1].joints[1].y*yScale;
            case 'Spine Shoulder X': return jsonObject.bodies[1].joints[20].x*xScale;
            case 'Spine Shoulder Y': return jsonObject.bodies[1].joints[20].y*yScale;
            case 'Left Thumb X': return jsonObject.bodies[1].joints[22].x*xScale;
            case 'Left Thumb Y': return jsonObject.bodies[1].joints[22].y*yScale;
            case 'Right Thumb X': return jsonObject.bodies[1].joints[24].x*xScale;
            case 'Right Thumb Y': return jsonObject.bodies[1].joints[24].y*yScale;
            case 'Left Wrist X': return jsonObject.bodies[1].joints[6].x*xScale;
            case 'Left Wrist Y': return jsonObject.bodies[1].joints[6].y*yScale;
            case 'Right Wrist X': return jsonObject.bodies[1].joints[10].x*xScale;
            case 'Right Wrist Y': return jsonObject.bodies[1].joints[10].y*yScale;
        }
    };
    
    ext.k = function(m) {
        switch(m){
            case 'Left Ankle X': return jsonObject.bodies[0].joints[14].x*xScale;
            case 'Left Ankle Y': return jsonObject.bodies[0].joints[14].y*yScale;
            case 'Right Ankle X': return jsonObject.bodies[0].joints[18].x*xScale;
            case 'Right Ankle Y': return jsonObject.bodies[0].joints[18].y*yScale;
            case 'Left Elbow X': return jsonObject.bodies[0].joints[5].x*xScale;
            case 'Left Elbow Y': return jsonObject.bodies[0].joints[5].y*yScale;
            case 'Right Elbow X': return jsonObject.bodies[0].joints[9].x*xScale;
            case 'Right Elbow Y': return jsonObject.bodies[0].joints[9].y*yScale;
            case 'Left Foot X': return jsonObject.bodies[0].joints[15].x*xScale;
            case 'Left Foot Y': return jsonObject.bodies[0].joints[15].y*yScale;
            case 'Right Foot X': return jsonObject.bodies[0].joints[19].x*xScale;
            case 'Right Foot Y': return jsonObject.bodies[0].joints[19].y*yScale;
            case 'Left Hand X': return jsonObject.bodies[0].joints[7].x*xScale;
            case 'Left Hand Y': return jsonObject.bodies[0].joints[7].y*yScale;
            case 'Right Hand X': return jsonObject.bodies[0].joints[11].x*xScale;
            case 'Right Hand Y': return jsonObject.bodies[0].joints[11].y*yScale;
            case 'Left Hand Tip X': return jsonObject.bodies[0].joints[21].x*xScale;
            case 'Left Hand Tip Y': return jsonObject.bodies[0].joints[21].y*yScale;
            case 'Right Hand Tip X': return jsonObject.bodies[0].joints[23].x*xScale;
            case 'Right Hand Tip Y': return jsonObject.bodies[0].joints[23].y*yScale;
            case 'Head X': return jsonObject.bodies[0].joints[3].x*xScale;
            case 'Head Y': return jsonObject.bodies[0].joints[3].y*yScale;
            case 'Left Hip X': return jsonObject.bodies[0].joints[12].x*xScale;
            case 'Left Hip Y': return jsonObject.bodies[0].joints[12].y*yScale;
            case 'Right Hip X': return jsonObject.bodies[0].joints[16].x*xScale;
            case 'Right Hip Y': return jsonObject.bodies[0].joints[16].y*yScale;
            case 'Left Knee X': return jsonObject.bodies[0].joints[13].x*xScale;
            case 'Left Knee Y': return jsonObject.bodies[0].joints[13].y*yScale;
            case 'Right Knee X': return jsonObject.bodies[0].joints[17].x*xScale;
            case 'Right Knee Y': return jsonObject.bodies[0].joints[17].y*yScale;
            case 'Neck X': return jsonObject.bodies[0].joints[2].x*xScale;
            case 'Neck Y': return jsonObject.bodies[0].joints[2].y*yScale;
            case 'Left Shoulder X': return jsonObject.bodies[0].joints[4].x*xScale;
            case 'Left Shoulder Y': return jsonObject.bodies[0].joints[4].y*yScale;
            case 'Right Shoulder X': return jsonObject.bodies[0].joints[8].x*xScale;
            case 'Right Shoulder Y': return jsonObject.bodies[0].joints[8].y*yScale;
            case 'Spine Base X': return jsonObject.bodies[0].joints[0].x*xScale;
            case 'Spine Base Y': return jsonObject.bodies[0].joints[0].y*yScale;
            case 'Spine Middle X': return jsonObject.bodies[0].joints[1].x*xScale;
            case 'Spine Middle Y': return jsonObject.bodies[0].joints[1].y*yScale;
            case 'Spine Shoulder X': return jsonObject.bodies[0].joints[20].x*xScale;
            case 'Spine Shoulder Y': return jsonObject.bodies[0].joints[20].y*yScale;
            case 'Left Thumb X': return jsonObject.bodies[0].joints[22].x*xScale;
            case 'Left Thumb Y': return jsonObject.bodies[0].joints[22].y*yScale;
            case 'Right Thumb X': return jsonObject.bodies[0].joints[24].x*xScale;
            case 'Right Thumb Y': return jsonObject.bodies[0].joints[24].y*yScale;
            case 'Left Wrist X': return jsonObject.bodies[0].joints[6].x*xScale;
            case 'Left Wrist Y': return jsonObject.bodies[0].joints[6].y*yScale;
            case 'Right Wrist X': return jsonObject.bodies[0].joints[10].x*xScale;
            case 'Right Wrist Y': return jsonObject.bodies[0].joints[10].y*yScale;
        }
    };

    
        
    // Register the extension
    ScratchExtensions.register('KinectinScratch', descriptor, ext);
})({});