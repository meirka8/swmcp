import win32com.client
import pythoncom


def connect_to_solidworks():
    """Connect to SolidWorks application."""
    # Ensure COM is initialized for the current thread
    try:
        pythoncom.CoInitialize()
        sw_app = win32com.client.Dispatch("SldWorks.Application")
        # Optional: Make SolidWorks visible
        # sw_app.Visible = True
        print("Successfully connected to SolidWorks")
        return sw_app
    except Exception as e:
        print(f"Failed to connect to SolidWorks: {e}")
        try:  # Attempt to get an already running instance if Dispatch failed
            sw_app = win32com.client.GetActiveObject("SldWorks.Application")
            # sw_app.Visible = True
            print("Successfully connected to an existing SolidWorks instance")
            return sw_app
        except Exception as e_active:
            print(f"Failed to get active SolidWorks instance: {e_active}")
            return None
