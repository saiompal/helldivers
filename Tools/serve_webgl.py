"""Serve the built game locally: python Tools/serve_webgl.py --port 8765.

Loopback only. Supports Unity's uncompressed and gzip/Brotli build artifacts.
"""
from argparse import ArgumentParser
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class UnityHandler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        if path.endswith(('.gz', '.br')):
            path = path.rsplit('.', 1)[0]
        if path.endswith('.wasm'):
            return 'application/wasm'
        if path.endswith('.data'):
            return 'application/octet-stream'
        return super().guess_type(path)

    def end_headers(self):
        if self.path.split('?', 1)[0].endswith('.gz'):
            self.send_header('Content-Encoding', 'gzip')
        elif self.path.split('?', 1)[0].endswith('.br'):
            self.send_header('Content-Encoding', 'br')
        self.send_header('Cross-Origin-Opener-Policy', 'same-origin')
        self.send_header('Cross-Origin-Embedder-Policy', 'require-corp')
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()


if __name__ == '__main__':
    parser = ArgumentParser()
    parser.add_argument('--port', type=int, default=8765)
    args = parser.parse_args()
    docs = Path(__file__).resolve().parents[1] / 'docs'
    server = ThreadingHTTPServer(('127.0.0.1', args.port), partial(UnityHandler, directory=str(docs)))
    print(f'IN THE DARK: http://127.0.0.1:{args.port}/', flush=True)
    server.serve_forever()
