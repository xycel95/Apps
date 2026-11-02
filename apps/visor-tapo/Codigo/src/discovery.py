"""Bounded, unauthenticated ONVIF discovery on private IPv4 interfaces."""
import ipaddress
import select
import socket
import time
import uuid
import xml.etree.ElementTree as ET
from urllib.parse import urlsplit, unquote
from core import LOCAL_NETWORKS


def parse_response(data, peer):
    try:
        address = ipaddress.ip_address(peer)
        if not any(address in network for network in LOCAL_NETWORKS):
            return []
        if len(data) > 60000 or b'<!DOCTYPE' in data.upper() or b'<!ENTITY' in data.upper():
            return []
        root = ET.fromstring(data)
        matches = root.findall('.//{http://schemas.xmlsoap.org/ws/2005/04/discovery}ProbeMatch')
        results = []
        for match in matches:
            xaddrs = match.find('{http://schemas.xmlsoap.org/ws/2005/04/discovery}XAddrs')
            if xaddrs is None:
                continue
            for url in (xaddrs.text or '').split():
                host = urlsplit(url).hostname
                # Never follow a device-supplied URL or discover arbitrary hosts.
                if host != peer:
                    continue
                scopes = match.find('{http://schemas.xmlsoap.org/ws/2005/04/discovery}Scopes')
                name = 'Cámara ONVIF'
                for scope in (scopes.text or '').split() if scopes is not None else []:
                    if '/name/' in scope or '/hardware/' in scope:
                        name = unquote(scope.rsplit('/', 1)[-1])[:80]
                        break
                results.append({'host': peer, 'name': ''.join(c for c in name if c.isprintable())})
        return results
    except (ValueError, ET.ParseError):
        return []


def discover(seconds=6):
    addresses = set()
    for entry in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET):
        host = entry[4][0]
        if any(ipaddress.ip_address(host) in network for network in LOCAL_NETWORKS):
            addresses.add(host)
    if not addresses:
        raise RuntimeError('No hay una interfaz de red privada disponible.')
    sockets = []
    found = {}
    try:
        for host in sorted(addresses):
            try:
                sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                sock.bind((host, 0))
                sock.setsockopt(socket.IPPROTO_IP, socket.IP_MULTICAST_IF, socket.inet_aton(host))
                sock.setsockopt(socket.IPPROTO_IP, socket.IP_MULTICAST_TTL, 1)
                sock.setblocking(False)
                sockets.append(sock)
            except OSError:
                if 'sock' in locals() and sock not in sockets:
                    sock.close()
        if not sockets:
            raise RuntimeError('No se ha podido iniciar la búsqueda local. Revisa el permiso de red de Windows.')
        for _ in range(2):
            for sock in sockets:
                message = f'''<s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:a="http://schemas.xmlsoap.org/ws/2004/08/addressing" xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery" xmlns:dn="http://www.onvif.org/ver10/network/wsdl"><s:Header><a:MessageID>uuid:{uuid.uuid4()}</a:MessageID><a:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</a:To><a:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</a:Action></s:Header><s:Body><d:Probe><d:Types>dn:NetworkVideoTransmitter</d:Types></d:Probe></s:Body></s:Envelope>'''
                try:
                    sock.sendto(message.encode(), ('239.255.255.250', 3702))
                except OSError:
                    pass
            end = time.monotonic() + seconds / 2
            while time.monotonic() < end:
                ready, _, _ = select.select(sockets, [], [], min(.3, max(0, end - time.monotonic())))
                for sock in ready:
                    try:
                        packet, peer = sock.recvfrom(65535)
                        for device in parse_response(packet, peer[0]):
                            found[device['host']] = device
                    except OSError:
                        continue
        return {'devices': sorted(found.values(), key=lambda d: d['host'])}
    finally:
        for sock in sockets:
            sock.close()
