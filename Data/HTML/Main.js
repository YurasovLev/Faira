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
    document.getElementById("Chat").textContent += message.data + "\n";
  }
} else alert("ERROR\nYour browser is not supported!")

function send(t) {
  ws.send(t.value)
  t.value = ""
}