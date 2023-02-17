if ("WebSocket" in window) {
  var localStorage = window.localStorage;
  var ws = new WebSocket(`ws://${document.location.host}//WebSocket`);

  ws.onopen = ()=>{
    console.log("Start WebSocket")
  }

  ws.onclose = ()=>{
    console.log("WebSocket close")
    document.getElementById("Chat").textContent = "Disconnected from server\n" + document.getElementById("Chat").textContent;
  }

  ws.onerror = (err) => { 
    throw err
  }

  ws.onmessage = (message)=>{
    console.log(message.data)
    document.getElementById("Chat").textContent = message.data + "\n" + document.getElementById("Chat").textContent;
  }
} else alert("ERROR\nYour browser is not supported!")

function send(t) {
  ws.send(t.value)
  t.value = ""
}