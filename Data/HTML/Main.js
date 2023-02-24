if ("WebSocket" in window) {
  var localStorage = window.localStorage;
  var ws = new WebSocket(`ws://${document.location.host}//WebSocket`);

  ws.onopen = ()=>{
    login()
    console.log("Start WebSocket")
  }

  ws.onclose = (e)=>{
    console.log("WebSocket close")
    // console.log(e)
    document.getElementById("Chat").textContent = "Disconnected from server\n" + document.getElementById("Chat").textContent;
  }

  ws.onerror = (err) => { 
    console.log(err)
    throw err
  }

  ws.onmessage = (message)=>{
    console.log(message.data)
    let data = JSON.parse(message.data)
    console.log(data)
    switch(data.Type) {
      case "Text":
        pastMessage(`<div style="color:white">${data.Content}</div>`)
        break;
      case "Info":
        pastMessage(`<div style="color:Lightblue">${data.Content}</div>`)
        break;
      case "Error":
        // pastMessage(`<div style="color:red">${data.Content}</div>`)
        if(data.Content == "User is not found" || "Incorrect password") {
          pastMessage(`<div style="color:red">Вы не вошли в систему, через 3 секунды вы будете направлены на страницу входа.</div>`)
          setTimeout(() => document.location.href = document.location.origin + "/Login.html", 3000)
        }
        break;
    }
  }
} else alert("ERROR\nYour browser is not supported!")

let MyID = localStorage.getItem("ID");

function login() {
  ws.send(">Login")
  let data = JSON.stringify({
    Password: localStorage.getItem("Password"),
    ID: MyID
  })
  ws.send(data)
}

function send(t) {
  ws.send(">Message")
  let data = JSON.stringify({
    Author: MyID,
    Content: t.value,
    Type: "Text"
  })
  ws.send(data)
  t.value = ""
}

let chat = document.getElementById("Chat")
function pastMessage(msg) {
  chat.innerHTML = msg + chat.innerHTML
}