const KEY = "spi_device_id";

function randomId() {
  return `d_${crypto.randomUUID?.() || Math.random().toString(36).slice(2)}`;
}

export function getDeviceId(): string {
  try {
    let id = localStorage.getItem(KEY);
    if (!id) {
      id = randomId();
      localStorage.setItem(KEY, id);
    }
    return id;
  } catch {
    return randomId();
  }
}
