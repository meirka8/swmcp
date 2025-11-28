import subprocess
import json
import sys
import os

def send_message(process, message):
    json_msg = json.dumps(message)
    content_length = len(json_msg.encode('utf-8'))
    # Write header and message
    process.stdin.write(f"Content-Length: {content_length}\r\n\r\n".encode('utf-8'))
    process.stdin.write(json_msg.encode('utf-8'))
    process.stdin.flush()

def read_message(process):
    # Read header
    header = ""
    while True:
        char = process.stdout.read(1).decode('utf-8')
        if not char:
            return None
        header += char
        if header.endswith("\r\n\r\n"):
            break
    
    # Parse content length
    content_length = 0
    for line in header.split("\r\n"):
        if line.startswith("Content-Length:"):
            content_length = int(line.split(":")[1].strip())
            break
            
    if content_length == 0:
        return None
        
    # Read body
    body = process.stdout.read(content_length).decode('utf-8')
    return json.loads(body)

def main():
    # Path to the server executable
    exe_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "../src/server/bin/Debug/net8.0-windows/server.exe"))
    
    print(f"Starting server: {exe_path}")
    
    # Start the server process
    process = subprocess.Popen(
        [exe_path],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=sys.stderr, # Forward stderr to console
        bufsize=0 # Unbuffered
    )

    try:
        # 1. Initialize
        print("Sending initialize...")
        send_message(process, {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "test-client", "version": "1.0"}
            }
        })
        
        response = read_message(process)
        print("Initialize response:", json.dumps(response, indent=2))
        
        # 2. Initialized notification
        send_message(process, {
            "jsonrpc": "2.0",
            "method": "notifications/initialized",
            "params": {}
        })

        # 3. Call GetPartInfo
        print("\nCalling GetPartInfo...")
        send_message(process, {
            "jsonrpc": "2.0",
            "id": 2,
            "method": "tools/call",
            "params": {
                "name": "GetPartInfo",
                "arguments": {}
            }
        })
        
        response = read_message(process)
        print("GetPartInfo response:", json.dumps(response, indent=2))

    except Exception as e:
        print(f"Error: {e}")
    finally:
        process.terminate()

if __name__ == "__main__":
    main()
