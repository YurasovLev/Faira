if ("WebSocket" in window) {

  var ws = new WebSocket(`ws://localhost:2021/`);

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
    console.log(message.data)
  }
} else alert("ERROR\nYour browser is not supported!")