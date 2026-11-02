import unittest
from discovery import parse_response

RESPONSE = b'''<s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery"><s:Body><d:ProbeMatches><d:ProbeMatch><d:Scopes>onvif://www.onvif.org/name/Tapo%20C230</d:Scopes><d:XAddrs>http://192.168.1.50:2020/onvif/device_service</d:XAddrs></d:ProbeMatch></d:ProbeMatches></s:Body></s:Envelope>'''

class DiscoveryTests(unittest.TestCase):
    def test_camera_name_and_local_address_are_extracted(self):
        self.assertEqual(parse_response(RESPONSE, '192.168.1.50'),
                         [{'host': '192.168.1.50', 'name': 'Tapo C230'}])

    def test_packet_cannot_redirect_to_a_different_device(self):
        self.assertEqual(parse_response(RESPONSE, '192.168.1.51'), [])
        self.assertEqual(parse_response(RESPONSE, '8.8.8.8'), [])

    def test_malformed_or_entity_based_xml_is_ignored(self):
        for packet in (b'not XML', b'<!DOCTYPE x>'+RESPONSE,
                       b'<!ENTITY x "x">'+RESPONSE, b'x'*65536):
            self.assertEqual(parse_response(packet, '192.168.1.50'), [])
