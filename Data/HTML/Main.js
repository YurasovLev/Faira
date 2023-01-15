if ("WebSocket" in window) {

  var ws = new WebSocket(`ws://${document.location.host}`);

  ws.onopen = ()=>{
    console.log("Start WebSocket")
  }

  ws.onclose = ()=>{
    console.log("WebSocket close")
  }

  ws.onerror = (err) => { 
    throw err
  }

  ws.onmessage = (message)=>{
    console.log(message)
  }
} else alert("ERROR\nYour browser is not supported!")